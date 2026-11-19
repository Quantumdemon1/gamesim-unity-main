namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the star pool's rhythm talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableStarRhythm
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "star.r.silence", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(two new players stop talking as they walk into the kitchen) \"Hear that? That's what my name sounds like in a new house.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Want me to leave so they can finish?\"",
                        Lands = "\"Ha. Stay. Let them sweat. It's good for them.\"",
                        Even = "\"Stay. They'll pick it back up the second I'm gone.\"",
                        Grates = "\"Don't make it a bit. It's already a bit.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Does it ever stop?\"",
                        Lands = "\"Around week three. Then it gets worse, quietly. Thanks for asking.\"",
                        Even = "\"No. You just stop hearing it.\"",
                        Grates = "\"You're new. You'll find out what stops and what doesn't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Means they're scared of you. Use it.\"",
                        Lands = "\"Scared's useful for a week. Then it gets you nominated. Good read, though.\"",
                        Even = "\"Use it for what? Scared people vote together.\"",
                        Grates = "\"Spoken like someone who's never been the name in the whisper.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.gap", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(at the dinner table, an empty seat on each side of them) \"Notice nobody sits next to the returning player on day one? They think it's catching.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "(sit down beside them) \"Then I'll catch it.\"",
                        Lands = "\"Brave. Or reckless. Either way the table just noticed you.\"",
                        Even = "\"Bold choice. The table noticed.\"",
                        Grates = "\"Now they'll think we're a voting bloc. Thanks for that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll give you some room.\"",
                        Lands = "\"Thoughtful. Wrong, but thoughtful. Room is what makes me look dangerous.\"",
                        Even = "\"Fine. Everyone does on day one.\"",
                        Grates = "\"So you're scared of it too. Noted.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Maybe they're right to keep their distance.\"",
                        Lands = "\"They are. That's what makes you sitting here interesting.\"",
                        Even = "\"Maybe. Distance never saved anyone in this game.\"",
                        Grates = "\"And yet here you are, talking to me. Pick a side of the table.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.whisper", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(hearing their own name from down the hall) \"That's my name in a whisper. Third time today. I keep count.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who was it this time?\"",
                        Lands = "\"The one who swore to my face an hour ago. Count's the useful part.\"",
                        Even = "\"Doesn't matter who. The count matters.\"",
                        Grates = "\"If I tell you, you're in the count too.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Want me to go make it four, but nicer?\"",
                        Lands = "\"Yes. Say something boring about me. Boring is safe.\"",
                        Even = "\"Sure. Keep it boring, though.\"",
                        Grates = "\"Nicer won't help. Nicer sounds like campaigning.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only three? You're slipping.\"",
                        Lands = "\"I know. Week one I'd have been at ten by lunch.\"",
                        Even = "\"It's early. Give them a few hours.\"",
                        Grates = "\"Laugh now. Wait until it's your name.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.memorywall", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(pointing at the memory wall) \"Spot the target. Hint: it's the face half the house has seen before.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Then you'd better make it worth their while to wait.\"",
                        Lands = "\"Exactly. That's my whole first month in a sentence.\"",
                        Even = "\"That's the plan. Waiting's cheaper for them than it looks.\"",
                        Grates = "\"I don't need a first-timer explaining my target to me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I thought it was the one with the bad photo.\"",
                        Lands = "\"That's also me. Two targets, one wall.\"",
                        Even = "\"Fine. Bad photo, big target. Same person.\"",
                        Grates = "\"Every photo on that wall is bad. That's the point.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you ever stop being the target?\"",
                        Lands = "\"When someone bigger shows up. So I go looking for someone bigger.\"",
                        Even = "\"Not really. You just get better at standing behind people.\"",
                        Grates = "\"Ask me that when you've worn one.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.shield", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(in the full sun of the yard) \"A veteran's first job isn't winning. It's finding someone bigger to stand beside.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Have you found them yet?\"",
                        Lands = "\"Two candidates. Neither knows. Don't tell them.\"",
                        Even = "\"Working on it. Ask me after the next comp.\"",
                        Grates = "\"If I had, I wouldn't say so standing next to you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You can stand beside me. I'm not that big.\"",
                        Lands = "\"That's sweet, and it's the worst pitch I've heard. I almost took it.\"",
                        Even = "\"Kind. Not big enough yet. Keep working.\"",
                        Grates = "\"You'd be a shield made of paper. Sorry. True.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So you use people as cover.\"",
                        Lands = "\"Everyone does. I'm just honest about the cover part.\"",
                        Even = "\"Cover's a strong word. Shade, maybe.\"",
                        Grates = "\"Say it louder. I'm sure someone wants a reason.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.pitch", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(setting the table for one more than usual) \"Someone's going to pitch taking out the veterans this week. Want to guess who?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Me. Obviously. I've been rehearsing.\"",
                        Lands = "\"Ha. Do the speech. If it's good I'll vote with you.\"",
                        Even = "\"You'd be terrible at it. Too friendly.\"",
                        Grates = "\"Don't joke about it. Somebody's not joking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"The one being nicest to you.\"",
                        Lands = "\"That's my guess too. You read faster than I expected.\"",
                        Even = "\"Probably. Could be the quiet one, though.\"",
                        Grates = "\"That's the easy answer. The easy answer is wrong in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'd rather not guess.\"",
                        Lands = "\"Fair. Guessing out loud is how you end up in the pitch.\"",
                        Even = "\"Smart. I'll guess for both of us.\"",
                        Grates = "\"Then you'll be surprised. Surprised is expensive here.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.heavier", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Week = 2 },
                Open = "(rocking the hammock with one foot) \"Here's the math on me. Every week I survive, the target gets heavier, not lighter.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"So every week you survive is also bad news.\"",
                        Lands = "\"Now you get it. Good news and bad news, same envelope.\"",
                        Even = "\"Pretty much. Bad news on a delay.\"",
                        Grates = "\"Thanks. Very comforting.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then win the next one and make it heavier.\"",
                        Lands = "\"That's the veteran answer. You've been watching.\"",
                        Even = "\"Maybe. Winning doesn't lighten anything.\"",
                        Grates = "\"Easy to say. You're not the one carrying it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Need a hand carrying it?\"",
                        Lands = "\"Careful. Help me carry it long enough and you're wearing it.\"",
                        Even = "\"Nice offer. It doesn't split.\"",
                        Grates = "\"That's how people end up on the block beside me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.tarps", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(watching tarps go up over the yard) \"Tarps in the yard. That's an endurance comp, or they want us to think it is.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Whatever it is, I hope it's yours.\"",
                        Lands = "\"Kind. I hope it's endurance. I've got patience to spare.\"",
                        Even = "\"Doesn't matter. I prepare for both.\"",
                        Grates = "\"Hoping for me sounds like you've given up on you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Maybe they're just painting the fence.\"",
                        Lands = "\"Nobody tarps a fence in a game this petty.\"",
                        Even = "\"Sure. And I'm here for the tan.\"",
                        Grates = "\"Keep it up and you'll be painting it yourself.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Whatever it is, I'm winning it.\"",
                        Lands = "\"Good. Say it once more, quieter. I want you meaning it, not announcing it.\"",
                        Even = "\"Big talk. I'll be standing next to you.\"",
                        Grates = "\"Announcing a win is how you get crowded out of one.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.sleep", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(glancing at the bedroom doors at dusk) \"Comp tip, free. Watch who goes to bed early the night before. Not who trains.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"So who usually goes to bed early?\"",
                        Lands = "\"Whoever wins. Funny how that works.\"",
                        Even = "\"The ones who knew. Funny how that works.\"",
                        Grates = "\"That's two tips. The second one isn't free.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Then I'm going to bed. Goodnight.\"",
                        Lands = "\"Ha. Fastest student I've had. Goodnight.\"",
                        Even = "\"Sure. I'll be up a while. Watching.\"",
                        Grates = "\"Going now is too obvious. Stay a bit. Then go.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You'd give that away? What's it really for?\"",
                        Lands = "\"Fair. It's to see who goes to bed early tonight. Like you might.\"",
                        Even = "\"Maybe I'm just generous. Maybe not.\"",
                        Grates = "\"I give you something and you audit it. Fine. No more free ones.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.loseto", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(stretching against the kitchen counter) \"Before a comp I decide who I'd rather lose to. Sounds strange. It isn't.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"So who would you rather lose to this week?\"",
                        Lands = "\"Someone who'll leave me off the block. Narrow field.\"",
                        Even = "\"Someone who owes me. That's all you get.\"",
                        Grates = "\"Clever. Not today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Me. Clearly. I'm very forgiving.\"",
                        Lands = "\"You're on the list. Low on it, but on it.\"",
                        Even = "\"You'd be fine. Don't make me regret it.\"",
                        Grates = "\"Forgiving people don't announce it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That's the most clear-eyed thing I've heard in here.\"",
                        Lands = "\"It's cheaper to choose a loss than to have one choose you.\"",
                        Even = "\"It's just math with a heart in it.\"",
                        Grates = "\"Clear-eyed is a polite word for cold. I'll let it slide.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.backyard", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(head tilted at the back door, the house quiet) \"They're building something out there. Stop talking a second. You can hear it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(go quiet and listen)",
                        Lands = "\"Hear that? Something with a hinge. Someone's going to fall off it.\"",
                        Even = "\"Hammering. Could be anything. Probably something that spins.\"",
                        Grates = "\"You're breathing loud. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Let's go look through the slats.\"",
                        Lands = "\"I've tried. They tape them. I like how you think, though.\"",
                        Even = "\"Taped. I checked. Good idea, too late.\"",
                        Grates = "\"And get caught on camera snooping? Go ahead. Alone.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Can you really tell what it is from the sound?\"",
                        Lands = "\"No. But the person who thinks I can just got nervous. That's the point.\"",
                        Even = "\"Sometimes. Mostly I just like being the one who listens.\"",
                        Grates = "\"I can tell what you're thinking. Close enough.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.alliances", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(counting on fingers by the pool) \"Week one, everybody has an alliance with everybody. I'm at six. How many are you in?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Honestly? Two. Maybe.\"",
                        Lands = "\"Two's honest. Two's also about right. Keep it there.\"",
                        Even = "\"Two's fine. Six is noise.\"",
                        Grates = "\"Maybe two. So you don't know. That's worse than six.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Seven. I'm beating you.\"",
                        Lands = "\"Ha. Then one of yours is fake. Find out which.\"",
                        Even = "\"Seven. Somebody's lying, and it might be you.\"",
                        Grates = "\"Then you're the one I'm worried about.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Which of your six are real?\"",
                        Lands = "\"One. Maybe. Ask me which at the second vote.\"",
                        Even = "\"That's the week-one question. Nobody knows yet.\"",
                        Grates = "\"Not the one asking.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.cocktail", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(watching the whole house laughing too hard in the kitchen) \"Week one's a cocktail party with a trapdoor. Everyone's charming. Somebody's falling.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"And you're the one who knows where the trapdoor is.\"",
                        Lands = "\"Roughly. I know where it was last time, anyway.\"",
                        Even = "\"Everyone thinks they do. Nobody does.\"",
                        Grates = "\"If I did, I wouldn't be standing this close to it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then stay away from the middle of the room with me.\"",
                        Lands = "\"Good instinct. The edges are where you see the trapdoor open.\"",
                        Even = "\"Fine. The edge it is.\"",
                        Grates = "\"Standing together in a corner is its own trapdoor.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll just enjoy the party, then.\"",
                        Lands = "\"Do. Seriously. It's the last easy week.\"",
                        Even = "\"Enjoy it. It ends Thursday.\"",
                        Grates = "\"That's what the person who falls usually says.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.loudest", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(a voice carrying down from upstairs) \"Week one, the loudest person in the house either leaves first or wins the whole thing. No middle.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So which one am I?\"",
                        Lands = "\"You're not the loudest. That's your first good decision.\"",
                        Even = "\"Neither. You're quiet. Different rules.\"",
                        Grates = "\"If you have to ask, you're louder than you think.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then I'll be loud on purpose and win.\"",
                        Lands = "\"Brave. Somebody has to test the rule.\"",
                        Even = "\"Bold. Most people who say that leave first.\"",
                        Grates = "\"That's exactly what the ones who leave first say.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Which way is it going this time?\"",
                        Lands = "\"Too early. But they just made their first enemy. Watch the stairs.\"",
                        Even = "\"Ask me after the first vote.\"",
                        Grates = "\"If I knew, I'd be planning around it. Quietly. Without you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.mic", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(straightening a crooked microphone pack) \"They'll make me fix this in a minute. You stop hearing the reminders after a week.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I still hear every one of them.\"",
                        Lands = "\"Good. Keep hearing them. The day you don't is the day you say the wrong thing.\"",
                        Even = "\"You will for a while. Then you won't.\"",
                        Grates = "\"That's why you sound so careful. Loosen up.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you ever forget you're wearing it?\"",
                        Lands = "\"Once a week. That's when people learn things about me. On purpose, usually.\"",
                        Even = "\"Sometimes. Not as often as people think.\"",
                        Grates = "\"Never. Which is why you'll never catch me forgetting.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You forget on purpose sometimes. I've seen it.\"",
                        Lands = "\"Ha. Caught. Sometimes forgetting is a move.\"",
                        Even = "\"Maybe. You'll never know which times.\"",
                        Grates = "\"And you're keeping notes on my mic. Interesting.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.followyou", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(eyes on a camera turning in the corner) \"That one just turned to follow you. Did you notice? Start noticing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I didn't. What does it mean?\"",
                        Lands = "\"Means you said something worth keeping. Figure out what.\"",
                        Even = "\"Means something. Not always what you'd hope.\"",
                        Grates = "\"Means you're the kind of player who doesn't notice. For now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "(wave at the camera)",
                        Lands = "\"Good. Own it. The ones who hide always look worse.\"",
                        Even = "\"Sure. Wave. It's still following.\"",
                        Grates = "\"And now that's the clip. Congratulations.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then I'll say something boring until it turns away.\"",
                        Lands = "\"Exactly right. Boring is a strategy. Underrated one.\"",
                        Even = "\"That works. For about a day.\"",
                        Grates = "\"Too boring and they'll make you interesting themselves.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.diary", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(coming back from the diary room, sitting down hard) \"Funny room. Everyone's wiser in there than they are out here. Me included.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So what genius thing did you say?\"",
                        Lands = "\"Something about you, actually. Nice things. Mostly.\"",
                        Even = "\"Can't tell you. It's better in my head anyway.\"",
                        Grates = "\"Ask the edit.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I won't ask what you said.\"",
                        Lands = "\"Thank you. Nobody ever doesn't ask.\"",
                        Even = "\"Good. It wasn't that good.\"",
                        Grates = "\"You can ask. I won't tell you. But you can ask.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So you're performing in there.\"",
                        Lands = "\"Everyone is. The trick is to perform the truth.\"",
                        Even = "\"A little. Everyone does.\"",
                        Grates = "\"Says the person performing being unimpressed right now.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.onesentence", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Week = 2 },
                Open = "(on the steps outside, quiet for once) \"You'll say one sentence this season that ends up defining you. You won't know which one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then I'll pick it myself.\"",
                        Lands = "\"Ha. Everybody tries. Some get close. Try.\"",
                        Even = "\"You can try. The edit picks.\"",
                        Grates = "\"That's the sentence that'll define you, by the way.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you know which one was yours?\"",
                        Lands = "\"I've got a guess. I didn't choose it. Nobody does.\"",
                        Even = "\"Some idea. Not the one I'd have picked.\"",
                        Grates = "\"Not something I share.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Here's hoping mine is a kind one.\"",
                        Lands = "\"That's a good wish. The kind ones last longest.\"",
                        Even = "\"Hope so. They don't usually pick those.\"",
                        Grates = "\"Kind's not a clip. You'll have to be something else too.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.grumpybench", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Week = 3 },
                Open = "(miming a row of crossed arms on the couch) \"Every move I make, I picture a jury bench watching it. A grumpy one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do they have snacks at least?\"",
                        Lands = "\"No snacks. That's why they're grumpy. Think about it.\"",
                        Even = "\"Never. That's half the grudge.\"",
                        Grates = "\"Joke about the jury now. Explain it to them later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Does that change what you do?\"",
                        Lands = "\"Every time. A move you can't explain isn't worth making late.\"",
                        Even = "\"Sometimes. Mostly how I do it, not what.\"",
                        Grates = "\"Some things I'd rather not tell a future juror.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Forget them. Play for the win and let them sulk.\"",
                        Lands = "\"That's one school. It works for someone every season.\"",
                        Even = "\"Brave. They remember the sulk, though.\"",
                        Grates = "\"That's how you make it to the end and lose there.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.explain", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Week = 3 },
                Open = "(watching the house from the back of the couch) \"Juries don't vote for who played. They vote for who can explain it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"So I should start practicing my explanation now.\"",
                        Lands = "\"Yes. Write it in your head every night. Change it every week.\"",
                        Even = "\"Not a bad idea. Just don't say it out loud yet.\"",
                        Grates = "\"Practicing now means you'll say it now. Don't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"That sounds like an excuse for losing a vote.\"",
                        Lands = "\"Ha. Maybe. It's also true. Both.\"",
                        Even = "\"Could be. Doesn't make it wrong.\"",
                        Grates = "\"Careful who you say that to. They'll remember the tone.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll take your word for it.\"",
                        Lands = "\"Good. Nobody believes me until they're sitting there.\"",
                        Even = "\"Fine. You'll find out yourself.\"",
                        Grates = "\"Don't take my word. Take notes.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.audience", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Week = 3 },
                Open = "(rinsing two glasses at the sink) \"Think about it. Everyone we evict becomes the audience who picks the winner. Strange game.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then I'll start auditioning tonight.\"",
                        Lands = "\"That's it. Say goodbye like you'll see them again. Because you will.\"",
                        Even = "\"Fine. Start by being decent at the door.\"",
                        Grates = "\"Audition out loud and you get a short goodbye.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then let's be kind on the way out the door.\"",
                        Lands = "\"That's the move most people forget. Kindness at the door.\"",
                        Even = "\"Decent is enough. Kind's a bonus.\"",
                        Grates = "\"Kind doesn't get you votes. Respect does.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So I should start buying them presents.\"",
                        Lands = "\"Tiny ones. Leftovers. They'd treasure leftovers.\"",
                        Even = "\"With what? We have nothing.\"",
                        Grates = "\"They'd see right through it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.halfadvice", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting on the arm of the couch) \"I've got a piece of advice for you. Half of it. The other half costs something.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What does the other half cost?\"",
                        Lands = "\"A vote, eventually. Not now. I'm patient.\"",
                        Even = "\"Something you'd notice. Later.\"",
                        Grates = "\"More than you've got right now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll take the free half. I'm cheap.\"",
                        Lands = "\"Ha. Free half: never be the first to say a name out loud.\"",
                        Even = "\"Free half: don't say names first.\"",
                        Grates = "\"The free half was that joke. You missed it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Keep it. I'll work it out.\"",
                        Lands = "\"Respect. Most people can't stand an unfinished sentence.\"",
                        Even = "\"Suit yourself. Offer's open.\"",
                        Grates = "\"You won't. But fine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.yourspot", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(refilling an ice tray) \"A new player asked what I'd do in their spot. I told them. Mostly true.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Mostly true. What was the part that wasn't?\"",
                        Lands = "\"Good catch. The part that helps me. There's always one.\"",
                        Even = "\"The part I'd need back later.\"",
                        Grates = "\"That's between me and them.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Would you tell me the true version?\"",
                        Lands = "\"Maybe. Ask me in a week. If you're still here, yes.\"",
                        Even = "\"Some of it. Depends on the week.\"",
                        Grates = "\"That's what they asked. They got the mostly-true one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Ask me next. I'll tell you what I'd do in yours.\"",
                        Lands = "\"Go on, then. I could use a fresh set of eyes.\"",
                        Even = "\"Go ahead. I'll be polite about it.\"",
                        Grates = "\"Thanks. I've had enough advice from first-timers.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.wished", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Week = 2 },
                Open = "(looking out at the yard through the glass) \"There's one thing I wish someone told me my first time in. I'm still deciding if I tell you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take your time. I'll be around.\"",
                        Lands = "\"That helps your case, actually.\"",
                        Even = "\"You'd better be.\"",
                        Grates = "\"Being around isn't the same as being trusted.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What would make you decide yes?\"",
                        Lands = "\"This. Asking it that way. Give me another week.\"",
                        Even = "\"Time. And you not telling anyone I said this.\"",
                        Grates = "\"Not asking so fast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is it 'don't trust the veteran'?\"",
                        Lands = "\"Ha. Close. It's 'trust them for the right things.'\"",
                        Even = "\"That's a different thing. Also true.\"",
                        Grates = "\"That's what you've decided already. Clear.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.hohfirst", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(watching the HoH room door) \"New key. Watch who knocks on that door first. Not with a snack. With a story.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's got the best story so far?\"",
                        Lands = "\"The one who hasn't gone up yet. Timing's half the story.\"",
                        Even = "\"Hard to say. Ask me who's second.\"",
                        Grates = "\"Mine. That's why I'm not going up yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Should I bring a snack and a story?\"",
                        Lands = "\"Both. Snack first. Nobody ever turns down a snack.\"",
                        Even = "\"Bring the snack. Skip the story.\"",
                        Grates = "\"Bring nothing. Nobody needs another visitor.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"And when are you going up?\"",
                        Lands = "\"After you. You'll tell me what I'm walking into.\"",
                        Even = "\"When it's quiet. Not before.\"",
                        Grates = "\"When you stop watching me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.sixdays", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(eyeing a cluster outside the HoH room) \"Everyone's nice to the new HoH for exactly six days. Then it's someone else's turn.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Should I be nice to them too?\"",
                        Lands = "\"Yes. Just be the one who's still nice on day seven.\"",
                        Even = "\"Sure. Everyone is.\"",
                        Grates = "\"If you have to ask, it'll show.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'm not going to fake being nice.\"",
                        Lands = "\"Good. They'll trust you more next week for it.\"",
                        Even = "\"Your call. Somebody notices either way.\"",
                        Grates = "\"Then you'll be honest and nominated.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll stay out of the way this week.\"",
                        Lands = "\"Smart. Out of the way is underrated.\"",
                        Even = "\"Fine. Just don't vanish.\"",
                        Grates = "\"Out of the way is exactly where they look for a pawn.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.lookedatme", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(stepping in from the yard, still breathing hard) \"When they handed over the key, three people looked straight at me. I counted.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Want me to count with you next time?\"",
                        Lands = "\"Yes. Two counts are better than one.\"",
                        Even = "\"Sure. Two pairs of eyes.\"",
                        Grates = "\"Then they'd be counting you too.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Three looking at you means three thinking of you.\"",
                        Lands = "\"Exactly. That's my week's work. Three people.\"",
                        Even = "\"Or thinking of the HoH. Hard to tell.\"",
                        Grates = "\"I know what it means. I asked who you saw.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Maybe they were looking at me.\"",
                        Lands = "\"Could be. Then we've both got a week.\"",
                        Even = "\"Maybe. That would be news.\"",
                        Grates = "\"Nobody's looking at you yet. Enjoy it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.relieved", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(still sitting at the nomination table after it's cleared) \"Two chairs, two names. New players watch the nominees. I watch who looks relieved.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Who looked relieved?\"",
                        Lands = "\"Two of them. One of them is going to be on the block next week.\"",
                        Even = "\"Two or three. Watch them this week.\"",
                        Grates = "\"You should've been watching. I'm not your replay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Me, probably. I was holding my breath.\"",
                        Lands = "\"Ha. I saw. It was a good one, too.\"",
                        Even = "\"Everyone was. Some just hid it.\"",
                        Grates = "\"Then stop holding it so loudly.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I wasn't looking at anyone.\"",
                        Lands = "\"That's fine. I was. That's what I'm for.\"",
                        Even = "\"That's most people.\"",
                        Grates = "\"That's what everyone says who was looking.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.sulk", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(glancing at the nominees across the yard) \"First thing a nominee decides is whether to campaign or sulk. Most choose wrong.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Which one would you choose?\"",
                        Lands = "\"Campaign quietly. Never sulk. Sulking is a whole week of losing.\"",
                        Even = "\"Campaign. Always campaign.\"",
                        Grates = "\"I'd choose not to answer a question that hypothetical.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Maybe someone should go and sit with them.\"",
                        Lands = "\"Go. They'll remember who came. That counts for later.\"",
                        Even = "\"Could. Careful, though. Sitting with them is a statement.\"",
                        Grates = "\"That's how you're on the block with them next week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Sulking can be a campaign, if it's done right.\"",
                        Lands = "\"It can. Very hard to do. Most people just look sad.\"",
                        Even = "\"Can be. Rarely is.\"",
                        Grates = "\"It isn't. That's a myth started by people who sulked.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.honestday", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(the house unusually quiet over dinner) \"Nominations day is the only honest day in here. Everybody's face shows. Look around.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then let's read them together. Right now.\"",
                        Lands = "\"All right. Start with the one who's eating fastest.\"",
                        Even = "\"Quietly. Faces notice faces.\"",
                        Grates = "\"Not out loud, not at dinner.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'd rather just eat.\"",
                        Lands = "\"Fair. Eat. Your face is showing too, though.\"",
                        Even = "\"Eat, then. I'll read.\"",
                        Grates = "\"Eating's a face as well. A guilty one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's my face saying?\"",
                        Lands = "\"Relieved, and a bit sorry about it. Good mix.\"",
                        Even = "\"Not much. That's a good thing.\"",
                        Grates = "\"That you'd rather I didn't look.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.realweek", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(stretching out on the yard grass) \"Veto's settled. Now the real week starts. The rest was setup.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What's the real week about this time?\"",
                        Lands = "\"Votes. Who has how many. I can count to two, so far.\"",
                        Even = "\"Votes. Always votes.\"",
                        Grates = "\"If I knew, I'd be at work, not on the grass.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Can I nap through the setup next time?\"",
                        Lands = "\"Ha. Napping through setup is a style. Some win with it.\"",
                        Even = "\"You might as well. Setup's mostly noise.\"",
                        Grates = "\"You'd sleep through your own nomination.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then let's get to work, both of us.\"",
                        Lands = "\"Good. Counting goes faster with two.\"",
                        Even = "\"Sure. Start with the people on the couch.\"",
                        Grates = "\"Slow down. The real week's for listening.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.clap", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(rinsing a mug slowly) \"At the veto meeting. Did you see who didn't clap?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Point them out. Right now.\"",
                        Lands = "\"Not with a finger. It's somebody who'll tell you they're happy for everyone.\"",
                        Even = "\"Somebody worth watching. That's all.\"",
                        Grates = "\"In the middle of the yard? Look next time yourself.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You were watching the claps instead of the meeting?\"",
                        Lands = "\"The claps are the meeting. The rest is the script.\"",
                        Even = "\"Both. The claps tell you more.\"",
                        Grates = "\"Better than watching the floor like you were.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I wasn't paying that much attention.\"",
                        Lands = "\"Then you missed the best part. Next time.\"",
                        Even = "\"Most weren't.\"",
                        Grates = "\"Meetings are where attention pays. Don't do that again.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.bestworst", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(watching the veto holder pace the yard) \"Holding that medal is the best week in the house and the worst choice in it. Look at them.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Want to go and talk them down?\"",
                        Lands = "\"No. But you should. It'll be remembered.\"",
                        Even = "\"Maybe later. Give them an hour.\"",
                        Grates = "\"That's a conversation that makes enemies on two sides.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"The choice is easy if you count the votes.\"",
                        Lands = "\"It is. Most people can't count when they're scared, though.\"",
                        Even = "\"Easy if you know the votes. Nobody does.\"",
                        Grates = "\"Spoken like someone who's never held it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'd use it. Every time.\"",
                        Lands = "\"Ha. Good. Somebody has to keep the house honest.\"",
                        Even = "\"Bold. Using it starts a fight, too.\"",
                        Grates = "\"Using it every time is how you stop getting picked to play.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.onebed", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(glancing at a bare bed in the corner) \"One less bed. Everybody sleeps better tonight and plays worse tomorrow.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why worse?\"",
                        Lands = "\"Because they think it's over. It's never over on eviction night.\"",
                        Even = "\"Too relaxed. You'll see it at breakfast.\"",
                        Grates = "\"If you don't see it, you're one of them.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So I should stay up all night to play well tomorrow.\"",
                        Lands = "\"A little. Don't sleep like you're safe.\"",
                        Even = "\"Sleep. Just sleep lightly.\"",
                        Grates = "\"No. You should stop making jokes about it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Better them than you, though.\"",
                        Lands = "\"Ha. Every week. That's the true sentence in this house.\"",
                        Even = "\"Better them than me. Say it quieter.\"",
                        Grates = "\"Careful. The cameras like that line too much.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.grudge", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Week = 4, Window = TalkWindows.AfterEviction },
                Open = "(looking at the front door after it shuts) \"The jury's got a new member. Grudge count goes up by one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Whose grudge, though?\"",
                        Lands = "\"Whoever voted them out cleanly. Strangely, that hurts least.\"",
                        Even = "\"Everyone's. In pieces.\"",
                        Grates = "\"Count your own. That's enough work.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Maybe they'll forgive by the finale.\"",
                        Lands = "\"Some do. The ones who were told the truth at the door.\"",
                        Even = "\"Some. Not most.\"",
                        Grates = "\"Sweet thought. Not how jurors work.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I don't want to think about the jury yet.\"",
                        Lands = "\"Good. Thinking about it too early makes you play scared.\"",
                        Even = "\"Fine. It'll think about you.\"",
                        Grates = "\"It's already thinking about you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.r.campaign", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(counting the empty chairs at the table) \"Nobody wants to be next. Watch who starts campaigning for HoH tonight.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Me. I'm starting now.\"",
                        Lands = "\"Good. Tell me your pitch tomorrow.\"",
                        Even = "\"Fine. Start with me, apparently.\"",
                        Grates = "\"Campaigning for HoH out loud is campaigning to be nominated.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Who usually starts first?\"",
                        Lands = "\"The one who's scared. Every time. Every season.\"",
                        Even = "\"Someone scared. Look for that.\"",
                        Grates = "\"You. Apparently.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Are you campaigning to me right now?\"",
                        Lands = "\"Ha. Maybe. Is it working?\"",
                        Even = "\"Maybe. Maybe I'm just talking.\"",
                        Grates = "\"If I was, you'd know. Relax.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.u.coffee", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(pouring coffee into two mugs) \"Coffee, then strategy. In that order. Every day I've ever spent in a house.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Make mine strong.\"",
                        Lands = "\"Strong. Like your game, if you keep this up.\"",
                        Even = "\"Strong it is.\"",
                        Grates = "\"You get it how it comes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What if I want strategy first?\"",
                        Lands = "\"Then you get bad strategy. Drink.\"",
                        Even = "\"Then you'll be wrong faster.\"",
                        Grates = "\"Then you're the reason nobody's awake in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Coffee's free. What does the strategy cost?\"",
                        Lands = "\"Less than you'd think. Drink first.\"",
                        Even = "\"Depends. Drink the free part.\"",
                        Grates = "\"More than you've got. Enjoy the coffee.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.u.whoyoutalked", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting cross-legged on the yard sofa) \"Same question as always. Who'd you talk to today, and who did all the talking?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Two or three people. They did most of the talking.\"",
                        Lands = "\"Good. Listening's the job. Keep doing it.\"",
                        Even = "\"Fine. Report back tomorrow.\"",
                        Grates = "\"Two or three. Very specific. Thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Nobody worth mentioning.\"",
                        Lands = "\"Fair. Some days are quiet. Those matter too.\"",
                        Even = "\"Then tomorrow will be busy.\"",
                        Grates = "\"Everyone's worth mentioning to someone.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Why do you ask me that every day?\"",
                        Lands = "\"Because you answer it straight. That's rare.\"",
                        Even = "\"Habit. And it's useful.\"",
                        Grates = "\"Because you answer it badly. Every day.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.u.readroom", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(on the couch, one eye on the hallway) \"Sit. Five minutes. Let's just read the room.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit and say nothing)",
                        Lands = "\"Perfect. You're better at this than half the house.\"",
                        Even = "\"Good. Quiet's half of reading.\"",
                        Grates = "\"You're fidgeting. That's a whole sentence.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's in the hallway right now?\"",
                        Lands = "\"Two people pretending they're not whispering. Good eye.\"",
                        Even = "\"Two. Neither interesting yet.\"",
                        Grates = "\"If you have to ask, you're not reading. You're asking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll start. Everyone's lying about something.\"",
                        Lands = "\"Ha. True. Now be more specific.\"",
                        Even = "\"Of course they are. Which part?\"",
                        Grates = "\"That's not a read. That's a fortune cookie.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.u.asked", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(leaning in the doorway) \"Running total. Today four people asked me for something. Guess how many I said yes to.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Zero. You like saying no.\"",
                        Lands = "\"Zero. You know me a little.\"",
                        Even = "\"Close. One. Don't tell anyone.\"",
                        Grates = "\"Wrong, and the joke's getting old.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Was one of them me?\"",
                        Lands = "\"Nope. You're the only one who never asks. Noted, and it helps.\"",
                        Even = "\"Not today. Tomorrow, probably.\"",
                        Grates = "\"You're asking now. So yes, five.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Say yes to me next and keep your streak up.\"",
                        Lands = "\"Ha. All right. One free yes. Spend it well.\"",
                        Even = "\"Maybe. Ask nicely.\"",
                        Grates = "\"That's how the streak becomes zero.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.u.lap", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(starting another slow lap of the yard) \"Walk a lap with me. Nobody listens in on a moving conversation.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Lead the way.\"",
                        Lands = "\"Good. Slow pace. We look like we're just walking.\"",
                        Even = "\"Slowly. That's the trick.\"",
                        Grates = "\"Too eager. People notice eager walkers.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What are we talking about?\"",
                        Lands = "\"Nothing. That's the point. Walking is the talk.\"",
                        Even = "\"Whatever you want. Just keep walking.\"",
                        Grates = "\"We're not, now. You said that too loud.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Who's listening that you're worried about?\"",
                        Lands = "\"Everyone. You too, but less, apparently.\"",
                        Even = "\"Nobody special. Habit.\"",
                        Grates = "\"You, a bit, right now.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.u.mood", Pool = TalkPools.Star, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(coming in from the yard, squinting at the house) \"I've been out there too long. What's the mood in here?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Tense. Someone ate the last of the good cereal.\"",
                        Lands = "\"That's how wars start in here. Seriously.\"",
                        Even = "\"Typical. Someone will pay for it.\"",
                        Grates = "\"That's always the mood. Give me the real one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Two people stopped talking to each other at lunch.\"",
                        Lands = "\"That's what I needed. Thank you. Which two?\"",
                        Even = "\"Noted. Happens every week.\"",
                        Grates = "\"Two always do. That's not news.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Quiet. Nothing much.\"",
                        Lands = "\"Quiet's worth knowing too. Thanks.\"",
                        Even = "\"Quiet's the worst kind.\"",
                        Grates = "\"Nothing much means you weren't looking.\"",
                    },
                },
            },
        };
    }
}
