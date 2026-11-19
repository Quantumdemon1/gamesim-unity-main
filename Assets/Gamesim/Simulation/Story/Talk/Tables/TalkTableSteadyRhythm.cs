namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the steady pool's rhythm talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableSteadyRhythm
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "steady.r.slop", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(stirring a pot of gray slop with grim focus) \"Trick with slop is you stop expecting it to be food. Then it's fine.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"And what is it, if not food?\"",
                        Lands = "\"A texture. A lifestyle. A test of character.\"",
                        Even = "\"Wallpaper paste with ambition.\"",
                        Grates = "\"Lunch. Yours too, if you lose the next one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Put some hot sauce in it. My treat.\"",
                        Lands = "\"That's the kindest thing anyone's done in here this week. Thank you.\"",
                        Even = "\"Hot sauce is gone. Somebody drank it. I have theories.\"",
                        Grates = "\"The hot sauce went three days ago. Don't get my hopes up.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'd rather lose a comp than eat that.\"",
                        Lands = "\"Everyone says that till day four. Then it's dinner.\"",
                        Even = "\"Then win. It's that simple.\"",
                        Grates = "\"Then win something. I'm tired of hearing it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.sloprecipes", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(writing on the back of a cereal box) \"I'm keeping slop recipes. Number four is slop with cinnamon. It's a crime, but it's my crime.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Number four sounds bad.\"",
                        Lands = "\"It is bad. That's why it's number four. Five's worse.\"",
                        Even = "\"It's slop. The bar is underground.\"",
                        Grates = "\"Then don't have number four. Nobody's making you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Give me the list. I'm on slop next, I can feel it.\"",
                        Lands = "\"Here. Don't skip three, it's the good one.\"",
                        Even = "\"Borrow it. Bring it back.\"",
                        Grates = "\"Win and you won't need it. Free tip.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're going to give somebody food poisoning.\"",
                        Lands = "\"Ha. Only the people who deserve it.\"",
                        Even = "\"Slop can't spoil. It was never fresh.\"",
                        Grates = "\"I'm making the best of a bad thing. Don't make it worse.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.lens", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(as a lens on the wall turns to follow them) \"That one follows me to the fridge every time. I've started saying good morning to it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Say it louder. Make them put it in the episode.\"",
                        Lands = "\"Tomorrow. Full voice.\"",
                        Even = "\"Mornings are quiet. I'll whisper it.\"",
                        Grates = "\"It's a small habit. Don't make it a show.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Smart. Keep the editors on your side.\"",
                        Lands = "\"Exactly. Nobody cuts the polite one.\"",
                        Even = "\"Maybe. It's mostly manners.\"",
                        Grates = "\"It's good morning. Not a campaign.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I've just stopped noticing them.\"",
                        Lands = "\"Lucky. I'll get there by week six.\"",
                        Even = "\"I'll get there.\"",
                        Grates = "\"You'll notice when one catches you picking your teeth.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.mic", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(fiddling with the mic pack at their waist) \"Third battery today. I swear this mic hears my stomach more than it hears me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Then lay off the coffee.\"",
                        Lands = "\"Ha. Fair. Good advice, terrible timing.\"",
                        Even = "\"Coffee's quiet. It's the slop.\"",
                        Grates = "\"I'll drink what I want. The mic can cope.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"At least they know you're alive.\"",
                        Lands = "\"That's one way to see it. I'll take it.\"",
                        Even = "\"True. Alive and gurgling.\"",
                        Grates = "\"Don't sweet-talk the mic situation. It's grim.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Unclip it for five minutes. Live a little.\"",
                        Lands = "\"And get called in for a talking-to? Tempting, though.\"",
                        Even = "\"Not worth the scolding.\"",
                        Grates = "\"You want to explain that in the diary room? Go ahead.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.earplugs", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(holding up a pair of foam earplugs) \"Found these in the storeroom. I'm guarding them with my life. Somebody in our room grinds their teeth like a pepper mill.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is it me? It's me, isn't it.\"",
                        Lands = "\"Not you. You whistle a bit. Different crime.\"",
                        Even = "\"No names. I'm a professional.\"",
                        Grates = "\"If you have to ask, I'm not telling you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Say the name. Everyone's thinking it.\"",
                        Lands = "\"Not a chance. But you're close.\"",
                        Even = "\"I'll take it to my grave. Or my eviction.\"",
                        Grates = "\"That's how drama starts. No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Split them. One each. Allies.\"",
                        Lands = "\"One earplug each. Worst alliance in history. I'm in.\"",
                        Even = "\"Half a plug. Half a deal.\"",
                        Grates = "\"These are mine. I'm not negotiating earplugs.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.sleeptalk", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(face down on a pillow at breakfast time) \"Didn't sleep. It's not the snoring. Somebody talks in their sleep and makes deals.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What deals?\"",
                        Lands = "\"Something about a final four and a tuna. Lot to unpack.\"",
                        Even = "\"Mumbled. Something about a vote.\"",
                        Grates = "\"Not telling. Sleep talk is private, even in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Write them down. Hold them to it.\"",
                        Lands = "\"Ha. I've got a page already.\"",
                        Even = "\"Tempting. They'd deny it.\"",
                        Grates = "\"Too tired for jokes. Ask me after coffee.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take my bed this afternoon. I'm up anyway.\"",
                        Lands = "\"You serious? That's huge. Thank you.\"",
                        Even = "\"Maybe. Thanks.\"",
                        Grates = "\"No naps. They wake you with the speaker. I'm not giving them the satisfaction.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.soup", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(lying flat on the yard concrete in the shade) \"It's so hot the pool is bath water. I got out to cool down.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Bath water with a view, though.\"",
                        Lands = "\"A view of a wall. With a camera on it.\"",
                        Even = "\"Low bar. I'll take it.\"",
                        Grates = "\"Don't make it sound nice. I'm dying.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "(bring a cold cloth from the kitchen)",
                        Lands = "\"You're a saint. I mean it. A saint.\"",
                        Even = "\"Ah. Thanks. It'll be warm in a minute, but thanks.\"",
                        Grates = "\"It's warm already. Everything's warm. Don't bother.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Stop complaining and come inside.\"",
                        Lands = "\"Fine. You win. Lead the way.\"",
                        Even = "\"Inside's worse. It's the fan.\"",
                        Grates = "\"I'll complain where I like. It's a free yard.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.fan", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(sharing the one fan with three other people) \"Four people, one fan. Closest thing to a real alliance this house has ever had.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take my spot. I'm cool enough.\"",
                        Lands = "\"Sliding over. Don't tell the other two.\"",
                        Even = "\"Thanks. Ten minutes, then it's mine again.\"",
                        Grates = "\"Nobody's cool in here. Don't lie to me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Whoever controls the fan controls the house.\"",
                        Lands = "\"Ha. I've got my hand on the switch.\"",
                        Even = "\"Then I'm in power, I guess.\"",
                        Grates = "\"It's a fan, not a vote.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Move over. I'm taking the middle.\"",
                        Lands = "\"Fine, but you're on turning-it-round duty.\"",
                        Even = "\"Middle's sweatier. Enjoy.\"",
                        Grates = "\"Wait your turn. There's a rota.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.threewords", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(coming out of the diary room, blinking) \"They asked for one word for each of us. I said 'nice' nine times. They wanted something else.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go back in and give them better words.\"",
                        Lands = "\"'Nice, but louder.' Better?\"",
                        Even = "\"I'm not going back in there today.\"",
                        Grates = "\"I said what I said. I'm not redoing it for anyone.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Nobody says 'nice' in there. That's rare.\"",
                        Lands = "\"That's what I thought. They didn't agree.\"",
                        Even = "\"Nice and boring. That's me.\"",
                        Grates = "\"I didn't do it for a gold star.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Give them something next time. It keeps you on screen.\"",
                        Lands = "\"Huh. Maybe. Something small.\"",
                        Even = "\"I'll think about it.\"",
                        Grates = "\"I'm not performing for a chair.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.diaryqueue", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(waiting outside the diary room door) \"Been out here twenty minutes. Whoever's in there is crying or giving a speech.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Time them. A long one means they're campaigning.\"",
                        Lands = "\"Ha. Twenty-two minutes. Campaigning, then.\"",
                        Even = "\"Or crying. Hard to say from out here.\"",
                        Grates = "\"I'm not timing people's feelings.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Go ahead of me when they're out.\"",
                        Lands = "\"You're sure? You're a good one.\"",
                        Even = "\"Thanks. I owe you one queue.\"",
                        Grates = "\"No, you were next. Rules are rules.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Knock. They've had long enough.\"",
                        Lands = "\"I would if I had the nerve.\"",
                        Even = "\"You knock. I'll watch.\"",
                        Grates = "\"Let people have their moment. You'll want yours.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.walk", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(looking up at the square of sky over the yard) \"First thing I'm doing outside: a walk. No reason. Just a walk, through a door that opens.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll walk with you.\"",
                        Lands = "\"Deal. No cameras. No agenda.\"",
                        Even = "\"Sure. Bring snacks.\"",
                        Grates = "\"Don't book me yet. I'm not out yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"A walk? That's it? Dream bigger.\"",
                        Lands = "\"Ha. Fine. A walk to somewhere that fries things.\"",
                        Even = "\"It's a long walk.\"",
                        Grates = "\"It's my dream. I'm allowed to keep it small.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Why wait? Lose the next vote and you can.\"",
                        Lands = "\"Nice try. I'll wait for the long way.\"",
                        Even = "\"I'll take the long road, thanks.\"",
                        Grates = "\"Funny. Don't joke about my eviction.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.rain", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(holding a mug, eyes closed) \"I miss bad weather. Isn't that stupid? Rain on a window. That's what I miss.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Not stupid. It's a good thing to miss.\"",
                        Lands = "\"Yeah. Thanks. You'd like rain on a window.\"",
                        Even = "\"Maybe. Ask me again when it's raining in here.\"",
                        Grates = "\"Don't. I'll get mushy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then come stand under the hose with me. Right now.\"",
                        Lands = "\"Ha. Fully dressed? Fine. Yes.\"",
                        Even = "\"Not the same. Close, though.\"",
                        Grates = "\"I don't want a hose. I want rain. Different thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You'll miss the sun the minute you're home.\"",
                        Lands = "\"Probably. I'm never happy. That's my charm.\"",
                        Even = "\"Maybe. Let me miss rain.\"",
                        Grates = "\"Let me have one thing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.list", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(adding to the shopping list on the fridge) \"Somebody's written 'hope' on this, between the eggs and the foil. I don't know what to do with that.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Underline it. Make it a priority.\"",
                        Lands = "\"Underlined. Twice. They can work it out.\"",
                        Even = "\"Done. Two lines, between eggs and foil.\"",
                        Grates = "\"Don't. Somebody meant it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Who wrote it?\"",
                        Lands = "\"Not telling. Same handwriting as 'more cheese', though.\"",
                        Even = "\"No idea. Neat handwriting, whoever it is.\"",
                        Grates = "\"Doesn't matter who. Leave it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Cross it off. It's not in the budget.\"",
                        Lands = "\"Ha. Fair. I'm leaving it, though. It's free.\"",
                        Even = "\"It doesn't cost anything. It can stay.\"",
                        Grates = "\"You'd cross off hope. Good to know.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.standing", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(balancing on one foot by the kettle) \"If it's an endurance comp, I've practiced standing. That's my whole plan. Standing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Standing isn't a plan. It's just standing.\"",
                        Lands = "\"Everybody says that till hour six.\"",
                        Even = "\"Simple works. Ask anyone who fell off first.\"",
                        Grates = "\"Then don't stand. Fall off first and see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Wait till they tell us what the comp is.\"",
                        Lands = "\"Smart. But this keeps my head busy.\"",
                        Even = "\"Maybe. It's free.\"",
                        Grates = "\"Plans are free. Let me have mine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll stand longer than you.\"",
                        Lands = "\"Ha. Go on, then. Right now.\"",
                        Even = "\"We'll see. Hour six, you and me.\"",
                        Grates = "\"That's not the point. I'm practicing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.washer", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting on the washing machine to keep it still) \"This thing walks across the floor on spin. Somebody has to ride it. Today it's me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Want me to take a turn?\"",
                        Lands = "\"Ten minutes each. Hold on at the end, it bucks.\"",
                        Even = "\"Next load. This one's nearly done.\"",
                        Grates = "\"It's a one-person machine. Just keep me company.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Has anyone told them it's broken?\"",
                        Lands = "\"Twice. They said it's fine. Watch it go.\"",
                        Even = "\"They know. It's been like this since the first week.\"",
                        Grates = "\"Tell them yourself. I'm busy being a weight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Let it walk. It's not your laundry.\"",
                        Lands = "\"Half of it's mine. And it walks into the door.\"",
                        Even = "\"It'd unplug itself. Then nobody gets clean socks.\"",
                        Grates = "\"Easy to say. Your shirts are in here too.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.lid", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(guarding a pot lid with one hand) \"Nobody touches this lid. It's resting. Every time somebody peeks, it starts over.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "(reach for the lid)",
                        Lands = "\"Hand. Off. Ha. You're lucky I like you.\"",
                        Even = "\"No. Same answer as everyone else got.\"",
                        Grates = "\"I said nobody. I meant nobody.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll wait with you.\"",
                        Lands = "\"Good. Patience. That's the secret ingredient.\"",
                        Even = "\"Sure. Ten more minutes.\"",
                        Grates = "\"Don't wait. Hovering's worse.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"How long till it's done? I'll plan around it.\"",
                        Lands = "\"Twenty minutes. Plan away.\"",
                        Even = "\"Twenty. Roughly.\"",
                        Grates = "\"Done when it's done.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.cups", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(on the floor, building a tower out of plastic cups) \"Day whatever. Eleven cups high. Twelve and I'm calling it a personal best.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I bet it falls at twelve.\"",
                        Lands = "\"You're on. Loser does the bins. Hold your breath.\"",
                        Even = "\"Bet taken. Loser makes the coffee.\"",
                        Grates = "\"Don't. Now it'll fall out of spite.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "(hand them a cup) \"Twelve. Go.\"",
                        Lands = "\"Steady hand. Okay. If it stands, it's half yours.\"",
                        Even = "\"It's leaning. It's leaning. It's fine.\"",
                        Grates = "\"Not that one. That's the cracked cup. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Let's do something stupid instead.\"",
                        Lands = "\"Like what? I'm in. Mostly in.\"",
                        Even = "\"Define stupid first. Then maybe.\"",
                        Grates = "\"Stupid gets you a slop pass. No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.solitaire", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(playing solitaire on the floor) \"I've won this four times. Cheating, obviously. There's nobody to stop me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"That's still cheating.\"",
                        Lands = "\"Ha. Report me. To who?\"",
                        Even = "\"I said obviously.\"",
                        Grates = "\"It's solitaire. Relax.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Deal me in. Two people can't cheat.\"",
                        Lands = "\"Two people can absolutely cheat. Sit.\"",
                        Even = "\"Sure. One game.\"",
                        Grates = "\"I was having a quiet hour.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What does that say about your game?\"",
                        Lands = "\"That I play to win. Same as you.\"",
                        Even = "\"Nothing. It's cards.\"",
                        Grates = "\"Not everything is a read. It's solitaire.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.spices", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(rearranging the spice rack by color) \"Alphabetical was yesterday. Today it's color. Tomorrow I don't know. Tomorrow scares me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Tomorrow, by how much you like them.\"",
                        Lands = "\"Paprika's on top, then.\"",
                        Even = "\"That's a short list.\"",
                        Grates = "\"Don't help. It's my one job.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Color's smarter. You find things faster.\"",
                        Lands = "\"Exactly. Somebody gets it.\"",
                        Even = "\"Faster for me, anyway.\"",
                        Grates = "\"It's not efficiency. It's boredom. Let me have it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll leave you to it.\"",
                        Lands = "\"Stay. Hand me the yellow ones.\"",
                        Even = "\"Sure. Leave the cumin out of it. It's been through enough.\"",
                        Grates = "\"Thanks. I need one job nobody argues about.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.wall", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(eating cereal facing the wall) \"I eat facing the wall now. If there's a camera on my face all day, breakfast gets the back of my head.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Hiding from them just makes them zoom in.\"",
                        Lands = "\"Ha. Then they get a close-up of my back.\"",
                        Even = "\"Probably. Still doing it.\"",
                        Grates = "\"It's breakfast. Let me have breakfast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You can't escape them, you know.\"",
                        Lands = "\"I know. It's a small rebellion. Let me have it.\"",
                        Even = "\"I know. Still facing the wall.\"",
                        Grates = "\"I know. You don't have to watch me too.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then your back's all they've got of you this morning.\"",
                        Lands = "\"Good. It's my best angle.\"",
                        Even = "\"Probably. They've got plenty of my face already.\"",
                        Grates = "\"Then they'll get to know my back. Leave it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.yawn", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(whispering) \"You ever forget they're watching, then remember right in the middle of a yawn?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Every day. Then I yawn bigger.\"",
                        Lands = "\"Ha. That's the spirit.\"",
                        Even = "\"Same. Mid-yawn, every time.\"",
                        Grates = "\"Show-off. Some of us still flinch.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Yawn straight at the lens next time. Own it.\"",
                        Lands = "\"Next one goes right down the barrel.\"",
                        Even = "\"Maybe. If I remember mid-yawn.\"",
                        Grates = "\"I'm not performing a yawn.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Nobody's watching that closely.\"",
                        Lands = "\"Somebody is. But you're right, mostly.\"",
                        Even = "\"Maybe. Somebody watched me eat a whole lemon.\"",
                        Grates = "\"Somebody always is. That's the point.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.splinter", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(on the yard step, working at a splinter with a safety pin) \"Third splinter from that bench. I've stopped complaining. Now I'm collecting.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Let me get it. Hold still.\"",
                        Lands = "\"Steady hands. Thanks. Number three, out.\"",
                        Even = "\"Careful. It's in deep. Ow. Okay. Okay.\"",
                        Grates = "\"I can do it. You're shaking the pin.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll stay off the bench, then.\"",
                        Lands = "\"Smart. The bench takes no prisoners.\"",
                        Even = "\"Good plan. Everyone sits on it anyway.\"",
                        Grates = "\"It's the best shade. You'll be back.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Report the bench. That's what they're for.\"",
                        Lands = "\"Ha. I did. They sent sandpaper. I'm sanding it.\"",
                        Even = "\"I did once. It's still a bench.\"",
                        Grates = "\"I'll handle my own splinters, thanks.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.socks", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(pulling a pair of socks off the line) \"These are my competition socks. Washed. Mostly. Nobody takes them off this line.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Mostly washed?\"",
                        Lands = "\"Rinsed. With intent.\"",
                        Even = "\"Clean enough for luck.\"",
                        Grates = "\"That's between me and the socks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll borrow them for the next one.\"",
                        Lands = "\"Over my dead body. Ha. Get your own luck.\"",
                        Even = "\"No. Luck doesn't lend.\"",
                        Grates = "\"Touch them and I'll know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"If you lose in them, are they retired?\"",
                        Lands = "\"Good question. I'd have to think about that. Seriously.\"",
                        Even = "\"One strike. Then retired.\"",
                        Grates = "\"Don't jinx the socks.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.sunscreen", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(lining up sunscreen bottles on the yard table) \"Six bottles, all nearly empty. Nobody finishes one. They just open the next.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So combine them. Problem solved.\"",
                        Lands = "\"Ha. Already started. Don't tell anyone it's three kinds.\"",
                        Even = "\"I tried. They don't mix. They separate.\"",
                        Grates = "\"You do it, then. I'm on bottle four.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Give me one. I'll finish it for you.\"",
                        Lands = "\"Finally. Somebody with follow-through. Here.\"",
                        Even = "\"Take the blue one. It's nearly done.\"",
                        Grates = "\"You'll open a new one. Everyone does.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Leave them. Somebody will sort it.\"",
                        Lands = "\"Somebody always does. It's me, and I don't mind.\"",
                        Even = "\"Somebody. Probably me, by Friday.\"",
                        Grates = "\"'Somebody' is how you get six bottles.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.hoh.letter", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(watching the new Head of Household climb the stairs with the key) \"Same key, same letter, same crying. I want one HoH who just reads it and says 'nice.'\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"That'd be you. 'Nice.' Door shut.\"",
                        Lands = "\"Exactly that. Then a nap in the big bed.\"",
                        Even = "\"Probably. I'd cry at the bed, though.\"",
                        Grates = "\"Don't put me in that room. I like my bunk.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You'd cry. Everyone cries.\"",
                        Lands = "\"Ha. Fine. One tear. Into the snacks.\"",
                        Even = "\"Maybe at the snacks.\"",
                        Grates = "\"Not on camera. I'd save it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"The letter's the soft spot. Watch who they hug after.\"",
                        Lands = "\"Good eye. That's the week, right there.\"",
                        Even = "\"Could be. Watch the second hug, too.\"",
                        Grates = "\"Let them have their letter. The game starts tomorrow.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.hoh.sandwich", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(making a sandwich, ignoring the party noise upstairs) \"Party in the HoH room. Somebody has to make lunch.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Hiding in the kitchen won't keep you off the block.\"",
                        Lands = "\"It's worked so far.\"",
                        Even = "\"It's a sandwich, not a hideout.\"",
                        Grates = "\"I'm making lunch. Not everything's a move.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go up there. Get your face seen.\"",
                        Lands = "\"Ha. After the sandwich. Priorities.\"",
                        Even = "\"They'll still be there in ten.\"",
                        Grates = "\"Don't send me up there like a delivery.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll skip it too.\"",
                        Lands = "\"Good. Quiet company's the best kind.\"",
                        Even = "\"Your call. There's bread if you want it.\"",
                        Grates = "\"Don't skip on my account.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.hoh.fridge", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(shaking out a rug, glancing up at the HoH window) \"New HoH, new rules. Somebody's about to want the fridge reorganized. I can feel it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Alphabetical by owner.\"",
                        Lands = "\"Don't give them ideas.\"",
                        Even = "\"Probably by color.\"",
                        Grates = "\"Don't joke. They'll hear it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Then tell them no.\"",
                        Lands = "\"I might. Politely. In a week.\"",
                        Even = "\"Not on day one.\"",
                        Grates = "\"Easy for you. You're not on fridge duty.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Every HoH thinks they're the first one to run the house.\"",
                        Lands = "\"And every one finds out by Thursday.\"",
                        Even = "\"Pretty much. Thursday is when the fridge gets moved back.\"",
                        Grates = "\"Careful. Walls are thin and the HoH has ears.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.noms.quiet", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(wiping a table nobody used) \"Two names on the screen and the whole house goes quiet like a library. Every single week.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You all right?\"",
                        Lands = "\"Yeah. Thanks for asking. Nobody does on this day.\"",
                        Even = "\"Fine. Keeping busy.\"",
                        Grates = "\"I'm wiping a table. I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's the quiet before the campaigning.\"",
                        Lands = "\"Exactly. Give it an hour, everybody's whispering.\"",
                        Even = "\"Pretty much. I give it till dinner.\"",
                        Grates = "\"I know what it is. I've been here as long as you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then make some noise. Put music on.\"",
                        Lands = "\"Ha. Read the room. But I like it.\"",
                        Even = "\"Not today. Maybe after the veto.\"",
                        Grates = "\"Not on nomination day. Have some sense.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.noms.soup", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(setting a pot of soup on the counter) \"Comfort food. The nominees won't touch it now. They'll finish it at two in the morning.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Can I have some at one?\"",
                        Lands = "\"One's fine. Leave them half.\"",
                        Even = "\"Half. Leave them half.\"",
                        Grates = "\"Not yours. Not today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Smart. Feed the nominees, they remember.\"",
                        Lands = "\"It's not that. But fine, they'll remember.\"",
                        Even = "\"Maybe. Mostly I just hate waste.\"",
                        Grates = "\"It's soup. Not a vote.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll leave it alone.\"",
                        Lands = "\"Thanks. You're a good one.\"",
                        Even = "\"Thanks. Two in the morning, it'll be gone.\"",
                        Grates = "\"No need to be noble about soup.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.noms.bathroom", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(toothbrush in hand, whispering coming from the next room) \"Nomination night. Everybody whispers in the bathroom. I just want to brush my teeth in peace.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Walk in and say good night. They'll scatter.\"",
                        Lands = "\"Ha. Done it before. Works every time.\"",
                        Even = "\"They'd only come back.\"",
                        Grates = "\"Don't make me the bathroom police.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Heard anything good?\"",
                        Lands = "\"Nothing I'll repeat. But yes.\"",
                        Even = "\"Just names. Same names.\"",
                        Grates = "\"I'm not your bathroom spy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Use the other sink. It's empty.\"",
                        Lands = "\"Smart. You're right.\"",
                        Even = "\"They'll follow me.\"",
                        Grates = "\"Why should I move? I live here too.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.veto.necklace", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(picking a dropped ribbon off the floor) \"That veto necklace weighs more than it looks. Ask anyone who's had to decide with it on.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Glad it wasn't you.\"",
                        Lands = "\"Me too. Honestly. Me too.\"",
                        Even = "\"This week, anyway.\"",
                        Grates = "\"Don't be glad yet. There's next week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"They made the right call.\"",
                        Lands = "\"I think so too. Hard one.\"",
                        Even = "\"Maybe. We'll find out Thursday.\"",
                        Grates = "\"You don't know that. Neither do I.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"They should've used it. Everyone knows it.\"",
                        Lands = "\"You'd say that to their face? Respect.\"",
                        Even = "\"Easy to say from the couch.\"",
                        Grates = "\"Not your necklace, not your call.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.veto.counting", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(watching through the yard door after the meeting) \"Meeting's done. Now we all walk around pretending we're not counting votes.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"I'm counting. Out loud, if you like.\"",
                        Lands = "\"Ha. Inside voice. But yes, me too.\"",
                        Even = "\"Count quieter.\"",
                        Grates = "\"Not out loud. Not near me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'm counting clouds. Totally different.\"",
                        Lands = "\"Same. Clouds are a great cover.\"",
                        Even = "\"Sure you are.\"",
                        Grates = "\"Count whatever you want. Just don't count at me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Not me. I'm off the clock.\"",
                        Lands = "\"Lucky. Teach me how.\"",
                        Even = "\"Sure. Tell me how that goes.\"",
                        Grates = "\"Nobody's off the clock in here.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.veto.teeth", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(dropping onto the couch, a long breath out) \"Veto meeting. Everyone in there smiled at everyone. I've never seen so many teeth.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Sit. I'll get you water.\"",
                        Lands = "\"You're kind. I'm all smiled out.\"",
                        Even = "\"Thanks. Cold, if there is any.\"",
                        Grates = "\"I'm fine. Don't fuss.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Watch whose smile stopped first.\"",
                        Lands = "\"I did. Second chair from the end. Remember that.\"",
                        Even = "\"Maybe. Smiles lie.\"",
                        Grates = "\"Let a smile be a smile for one hour.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"So what happens Thursday?\"",
                        Lands = "\"Ha. Ask me Wednesday.\"",
                        Even = "\"Couldn't tell you. Nobody can yet.\"",
                        Grates = "\"Can I sit for one minute first?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.evict.sock", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(holding up a single sock found under the couch) \"One of theirs. Do I send it after them? Is there a process for this?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Hang it on the line. They'd laugh.\"",
                        Lands = "\"They would. All right. It stays a day.\"",
                        Even = "\"A day, then. Then it's a duster.\"",
                        Grates = "\"It's a sock. Don't make it a shrine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Did you like them?\"",
                        Lands = "\"I did. Liked their laugh. That's the bit that goes.\"",
                        Even = "\"Enough to ask about the sock.\"",
                        Grates = "\"Doesn't matter now, does it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Whose side of the room gets their space now?\"",
                        Lands = "\"Already? Fair. I'd take the window side.\"",
                        Even = "\"Whoever wants it. Not me.\"",
                        Grates = "\"Can I hold the sock for an hour first?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.evict.plate", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(looking at one plate too many on the table) \"Set an extra plate. Habit. Takes about two days to stop.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll take the extra one.\"",
                        Lands = "\"Good. Then it's not wasted.\"",
                        Even = "\"Sure. Eat. It's still warm.\"",
                        Grates = "\"That's not the point. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Leave it there tonight.\"",
                        Lands = "\"Yeah. Tonight. Tomorrow it goes.\"",
                        Even = "\"Fine. One more night.\"",
                        Grates = "\"Then it's just weird. I'll put it back.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"They're not coming back. Put it away.\"",
                        Lands = "\"Yeah. You're right. Somebody had to say it.\"",
                        Even = "\"I know. Give it a day.\"",
                        Grates = "\"I know they're not. I said habit.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.r.evict.stillness", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(one lamp on, the house too still) \"First night after an eviction is the quietest of the week. Nobody plots. Not yet. Give it till breakfast.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Breakfast. That's generous.\"",
                        Lands = "\"Ha. Fine. Midnight.\"",
                        Even = "\"Maybe sooner.\"",
                        Grates = "\"Let me have my one quiet night.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So we plot now, while nobody's looking.\"",
                        Lands = "\"We're the only ones up. Don't tempt me.\"",
                        Even = "\"Cute. Go to bed.\"",
                        Grates = "\"Not tonight. Seriously.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Who's next, then?\"",
                        Lands = "\"Ha. Breakfast. I said breakfast.\"",
                        Even = "\"Ask me tomorrow.\"",
                        Grates = "\"Read the room. Literally anybody else.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.u.morning", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(passing in the hall with a mug) \"Morning. Rate it. One to ten.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"A seven. A good seven.\"",
                        Lands = "\"A good seven is a good day. Keep it.\"",
                        Even = "\"Seven, then. Noted.\"",
                        Grates = "\"Hm. You say seven like you mean four.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Four. Slightly worse than yesterday.\"",
                        Lands = "\"A four is still a day.\"",
                        Even = "\"Four. I'll bring you coffee.\"",
                        Grates = "\"Then don't spread it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Five. A little tired.\"",
                        Lands = "\"Me too. Sit if you want.\"",
                        Even = "\"Same here. Coffee's on.\"",
                        Grates = "\"We all are. Join the club.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.u.cards", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(shuffling a deck at the table) \"One hand? Stakes are nothing, as usual.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Nothing's a bit rich for me.\"",
                        Lands = "\"Big spender. Cut.\"",
                        Even = "\"Cut, then.\"",
                        Grates = "\"Every time. Just cut.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Just one.\"",
                        Lands = "\"One's all I've got in me. Cut.\"",
                        Even = "\"One. Then I've got dishes.\"",
                        Grates = "\"You always say one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Raise you a dish shift.\"",
                        Lands = "\"Ha. Now we're playing. Cut.\"",
                        Even = "\"No raising. Cut.\"",
                        Grates = "\"Stakes are nothing. That's the point.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.u.couch", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(on the couch, shifting over without looking away from the room) \"Room here.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "(sit) \"Thanks.\"",
                        Lands = "\"Course. Same spot as always.\"",
                        Even = "\"Mm. Watch the kitchen with me.\"",
                        Grates = "\"Mind the cushion. That side's mine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Good view of the kitchen from here.\"",
                        Lands = "\"Best seat in the house. Don't tell anyone.\"",
                        Even = "\"Sure. Best seat for it.\"",
                        Grates = "\"It's a couch. Just sit.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit without a word)",
                        Lands = "\"Good. Nobody needs to talk.\"",
                        Even = "\"Mm. Good.\"",
                        Grates = "\"You can talk. I'm not a library.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.u.anythingnew", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(drying a plate, not looking up) \"Anything new?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Nothing. You?\"",
                        Lands = "\"Nothing. Good. Nothing's good.\"",
                        Even = "\"Nope. Same plates, same people.\"",
                        Grates = "\"Nothing's new for anybody. Why do we even ask?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You'd know before I would.\"",
                        Lands = "\"Fair. I hear everything at this sink.\"",
                        Even = "\"Probably. The sink hears everything.\"",
                        Grates = "\"I don't gossip. I dry plates.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Somebody moved the good spoon.\"",
                        Lands = "\"Ha. That was me. It's safe.\"",
                        Even = "\"It's in the drawer.\"",
                        Grates = "\"Don't start a spoon war.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.u.hot", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(shading their eyes at the yard sky) \"Still hot.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Still hot.\"",
                        Lands = "\"Good. We agree on something.\"",
                        Even = "\"Yep. Hotter by the hour.\"",
                        Grates = "\"I know. I said that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"It'll break eventually.\"",
                        Lands = "\"Optimist. I like it.\"",
                        Even = "\"Sure. Some week.\"",
                        Grates = "\"You said that last week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Hotter than yesterday. I measured.\"",
                        Lands = "\"With what? Ha. I believe you.\"",
                        Even = "\"Feels it. My shirt agrees.\"",
                        Grates = "\"Don't. Measuring makes it worse.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.u.kettle", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(holding up the kettle) \"Tea. Don't overthink it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Yes, please.\"",
                        Lands = "\"Coming up. Same as always.\"",
                        Even = "\"Coming. Milk's nearly gone.\"",
                        Grates = "\"Stop saying please. We're past that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Make it strong this time.\"",
                        Lands = "\"Strong. Like you mean it.\"",
                        Even = "\"It's tea. It's always strong.\"",
                        Grates = "\"You get what I make.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You say that every time.\"",
                        Lands = "\"And every time you drink it.\"",
                        Even = "\"And you say that every time.\"",
                        Grates = "\"Then don't have any.\"",
                    },
                },
            },
        };
    }
}
