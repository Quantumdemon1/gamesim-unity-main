namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the steady pool's layers talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableSteadyLayers
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "steady.l1.bunk", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(shoving a bag under the bottom bunk) \"Bottom bunk's mine. I don't climb in the dark. Everybody's doing hometowns tonight, so go on, ask.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Welcome in. Tell me one thing about you that's true.\"",
                        Lands = "\"One true thing. Easy. Here.\"",
                        Even = "\"Sure. One thing, then bed.\"",
                        Grates = "\"One true thing on move-in night? Bit much. Ask me tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You've rehearsed this answer, haven't you?\"",
                        Lands = "\"Twice in the car. You get the polished version.\"",
                        Even = "\"Once. In the shower. Here it is.\"",
                        Grates = "\"Rehearsed? No. And now I'm not telling you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"I'll trade you. Your answer for mine.\"",
                        Lands = "\"Fair trade. I'll go first so you can't wriggle out.\"",
                        Even = "\"Fine. Me first. Don't forget you owe me.\"",
                        Grates = "\"It's a hometown, not a hostage swap. Night.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l1.firstcoffee", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(measuring coffee into a filter for the whole house) \"First morning. I make the coffee and learn one thing about each person. That's my system.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Black is fine. So what's the one thing about you?\"",
                        Lands = "\"Black. Simple. I like that. All right, here's mine.\"",
                        Even = "\"Noted. One about me, then.\"",
                        Grates = "\"You skipped yours and went straight to mine. That's not the system.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Don't let me hold up the coffee.\"",
                        Lands = "\"You're not. Pot takes four minutes. One thing while it drips.\"",
                        Even = "\"It's dripping anyway. Fine. One thing.\"",
                        Grates = "\"Okay. Grab a mug when it's done.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Skip the system. Tell me the real you.\"",
                        Lands = "\"The real me makes coffee. But all right, here's a bit more.\"",
                        Even = "\"The real me is mostly this. Here's a little extra.\"",
                        Grates = "\"It's seven in the morning. The real me isn't up yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l1.folding", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(folding a mountain of towels on the couch) \"Folding's my thinking time. Ask me something while my hands are busy. I answer better.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "(grab a towel and fold) \"Go on, then. I'm listening.\"",
                        Lands = "\"Look at that. Corners and everything. All right.\"",
                        Even = "\"Your corners are wrong. Fine. Here's something.\"",
                        Grates = "\"Those are triangles. Put it down. We'll talk another time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're not thinking. You're hiding in the laundry.\"",
                        Lands = "\"Ha. Caught. Fine, I'll come out of the laundry.\"",
                        Even = "\"Bit of both. Ask anyway.\"",
                        Grates = "\"I'm folding towels. Not everything in here is a hiding place.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What do people in here get wrong about you?\"",
                        Lands = "\"Good question. Most of it. Here's the right part.\"",
                        Even = "\"Some of it. I'll give you one piece.\"",
                        Grates = "\"Ask me that in a week. I don't know you yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l1.dishes", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(elbow-deep in the sink, nodding at a dish towel) \"You dry, I wash. That's the deal. Talking's optional, but it goes faster.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Talking costs extra.\"",
                        Lands = "\"Then I'm paying. Here's something about me, on the house.\"",
                        Even = "\"Put it on my tab. Here.\"",
                        Grates = "\"Then dry in silence. Suits me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Deal. I ask, you answer, nobody gets wet.\"",
                        Lands = "\"Smart. Ask away. I'll start with the easy bit.\"",
                        Even = "\"Fine. Dry that one again, though. Here.\"",
                        Grates = "\"That sounds like a contract. It's dishes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(just pick up the towel)",
                        Lands = "\"Good. The quiet ones get the good stories. Here's one.\"",
                        Even = "\"All right. I'll fill the quiet, then.\"",
                        Grates = "\"Fine. We'll just dry.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l1.toast", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(making toast at two in the morning, the house dark) \"Can't sleep. Toast helps. Sit, and I'll tell you something nobody's bothered to ask me yet.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're not up for toast. You're worried. Say it.\"",
                        Lands = "\"Fine. Sit. Something else first, though.\"",
                        Even = "\"Maybe. Here's a different thing.\"",
                        Grates = "\"I'm up because somebody snores. Night.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then I'll ask the thing nobody's asked.\"",
                        Lands = "\"Go on. I'll answer straight.\"",
                        Even = "\"Try it. You'll get most of it.\"",
                        Grates = "\"Easy. It's two in the morning, not a cross-examination.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Toast at two a.m. is already a confession.\"",
                        Lands = "\"Ha. Then here's the rest of it.\"",
                        Even = "\"Probably. Here's a smaller one.\"",
                        Grates = "\"It's a snack. Don't read into my snack.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l1.lounger", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(kneeling by a broken pool lounger, a butter knife for a screwdriver) \"Hold this leg still. You can ask me things while I pretend I know what I'm doing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Give me the knife. I'll fix it.\"",
                        Lands = "\"No. Hold the leg and I'll talk instead.\"",
                        Even = "\"Nope. Mine. Ask me something instead.\"",
                        Grates = "\"Hold the leg. That's your job.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Why fix it? They'll just bring a new one.\"",
                        Lands = "\"Because sitting still makes me talk. Like now. Listen.\"",
                        Even = "\"Habit. Here's another habit of mine.\"",
                        Grates = "\"Because it's broken and I'm here. That's the whole reason.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "(hold the leg exactly level)",
                        Lands = "\"Perfect. You'd be useful on a job. Okay, ask.\"",
                        Even = "\"Little left. Good. Go on.\"",
                        Grates = "\"Little left. No, the other left. Never mind, I've got it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l1.photo", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(in front of the memory wall, squinting at their own photo) \"Terrible picture. I look like I'm being booked. Ask me anything except about the picture.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So. About the picture.\"",
                        Lands = "\"Ha. Fine. You get one real answer for that.\"",
                        Even = "\"Nope. Something else, then. Here.\"",
                        Grates = "\"Every time. Every single person. I'm going.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's a good picture. You look like yourself.\"",
                        Lands = "\"That's the nicest lie in the house. Here's the truth instead.\"",
                        Even = "\"Thanks. Sort of. All right.\"",
                        Grates = "\"I look like a mugshot. Don't humor me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll take the picture's word for it.\"",
                        Lands = "\"Don't. Let me give you a better one.\"",
                        Even = "\"Well, it's wrong. Here.\"",
                        Grates = "\"Suit yourself.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l1.chores", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(writing the chore chart on the fridge whiteboard) \"Somebody has to do the chart. Nobody else would. Tell me what you're good at and I'll tell you about me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm good at avoiding chores.\"",
                        Lands = "\"Honest. I'll put you on bins. Here's mine.\"",
                        Even = "\"Bins it is. Me? Here.\"",
                        Grates = "\"Great. Bins. All week. We'll talk when they're empty.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Who made you the chore boss?\"",
                        Lands = "\"Nobody. That's how it works. Here's why I'm the type.\"",
                        Even = "\"Nobody did. Ask me why I do it anyway.\"",
                        Grates = "\"Nobody. Nobody else stepped up, either. Want the pen?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Put me down for whatever's left.\"",
                        Lands = "\"Whatever's left. My favorite kind of person. Here's my side.\"",
                        Even = "\"Done. Dishes Tuesday. Now, me.\"",
                        Grates = "\"Everyone says that, then nobody shows up. We'll see.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.pot", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(stirring a big pot, tasting, frowning) \"This is how it was made where I grew up. Wrong pot, wrong stove, wrong everything. Still close.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Who taught you?\"",
                        Lands = "\"Ah. Now you're asking the right thing.\"",
                        Even = "\"Long story. Short version, though.\"",
                        Grates = "\"Don't. I'll get sentimental, and there's a camera on the fridge.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Close enough to share?\"",
                        Lands = "\"Bowl's there. Eat, and I'll tell you where it's from.\"",
                        Even = "\"Small bowl. Here's the story with it.\"",
                        Grates = "\"It's for the house. Wait like everybody else.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Wrong pot's an excuse. It's just not very good.\"",
                        Lands = "\"It's not. You should've had it back home, though.\"",
                        Even = "\"Fair. You've never had it right. Here.\"",
                        Grates = "\"Then don't eat it. Simple.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.clock", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(glancing at a kitchen clock that's always wrong) \"No clock in here tells the real time. I keep doing sums. What time it is at home, what I'd be doing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"And what would you be doing?\"",
                        Lands = "\"Straight to it. All right.\"",
                        Even = "\"You'll laugh. Fine.\"",
                        Grates = "\"Something better than this. That's all you get tonight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You don't have to tell me.\"",
                        Lands = "\"I know. Which is why I want to.\"",
                        Even = "\"I'll tell you a bit.\"",
                        Grates = "\"Good. I wasn't going to.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Missing home's a bad look on camera.\"",
                        Lands = "\"Maybe. I don't care tonight. Listen.\"",
                        Even = "\"Probably. Here's why I don't mind.\"",
                        Grates = "\"So's handing out camera advice. I'll miss who I want.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.hum", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(wiping down the counter, humming, then stopping short) \"That song's from home. I didn't know I was humming it till you walked in.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Keep going. I want the chorus.\"",
                        Lands = "\"Ha. No chorus. But I'll tell you where it comes from.\"",
                        Even = "\"Not on command. The story behind it, though.\"",
                        Grates = "\"Now I'll hear myself all night. Thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's a nice one. Who sang it to you?\"",
                        Lands = "\"Good question. It's the realest thing about me. Here.\"",
                        Even = "\"Huh. Okay. Then you'll want the rest.\"",
                        Grates = "\"You don't have to be nice about my humming.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Hum it louder. Make the whole house learn it.\"",
                        Lands = "\"They'd ruin it. I'll tell you about it instead.\"",
                        Even = "\"Not louder. But here's something about it.\"",
                        Grates = "\"Don't push. It was a private hum.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.towel", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(pinning a wet towel to the yard fence) \"Heat and wet laundry. That smell takes me straight back. Don't ask where unless you want the whole thing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I want the whole thing.\"",
                        Lands = "\"Okay. You asked.\"",
                        Even = "\"The whole thing's long. Here's the middle.\"",
                        Grates = "\"Not that fast. I said unless.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Give me the short version. It's hot.\"",
                        Lands = "\"Short. Practical. I like that. Here.\"",
                        Even = "\"Short version it is.\"",
                        Grates = "\"Then we'll do it when it's not hot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You say 'don't ask' because you want someone to ask.\"",
                        Lands = "\"Ha. Busted. Sit down.\"",
                        Even = "\"Maybe. You asked, so.\"",
                        Grates = "\"I say 'don't ask' because I mean it. Usually.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.mirror", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(at the bathroom mirror, toothbrush stopped) \"Somebody I love is watching me through this glass right now. Strange thing to brush your teeth to.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Wave. They'd like that.\"",
                        Lands = "(a small wave at the mirror) \"There. Now you get to hear about them.\"",
                        Even = "\"Okay. One wave. Here's who.\"",
                        Grates = "\"I'm not waving at a mirror. I've got some dignity left.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then say something to them. Out loud.\"",
                        Lands = "\"Ha. Okay. Hi. There. Now you get to hear who.\"",
                        Even = "\"Not out loud. But here's who.\"",
                        Grates = "\"I'm not giving a speech to a sink.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll give you the room.\"",
                        Lands = "\"Stay. I'll tell you about them.\"",
                        Even = "\"You're fine. Here, quickly.\"",
                        Grates = "\"Thanks. Yeah.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.hinge", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(oiling a squeaky cupboard with cooking oil) \"I keep fixing things in here that aren't mine. Somebody back home would laugh their head off.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why would they laugh?\"",
                        Lands = "\"Because they know where I learned it. Here.\"",
                        Even = "\"Because of how I grew up. Short version.\"",
                        Grates = "\"Long story. Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You fix things so you don't have to talk to people.\"",
                        Lands = "\"Half right. I'm talking to you, though. Listen.\"",
                        Even = "\"Maybe. Here's the half that isn't.\"",
                        Grates = "\"Or I'm fixing a door. Not everything's a symptom.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Fix my bunk next and tell me the whole story.\"",
                        Lands = "\"Your bunk's a lost cause. The story isn't.\"",
                        Even = "\"Bunk later. Story now.\"",
                        Grates = "\"I'm not the house handyman. This was one hinge.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.button", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(sewing a button back on with a tiny travel kit) \"My hands learned this a long way from here. Somebody made me practice till it was neat.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "(pull up a chair and hold the thread)",
                        Lands = "\"Look at you. All right, here's where my hands learned it.\"",
                        Even = "\"Hold it still. Fine. Here's the story.\"",
                        Grates = "\"You're tangling it. Give it here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Handy skill. Half the house will want you now.\"",
                        Lands = "\"Let them. It's not a strategy, though. It's where I'm from.\"",
                        Even = "\"Maybe. That's not why I learned it.\"",
                        Grates = "\"It's a button. Not every skill is a move.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do mine next. I've lost three.\"",
                        Lands = "\"Three? Bring them. Story's free with the service.\"",
                        Even = "\"One at a time. Here's the story with it.\"",
                        Grates = "\"I'm not the house tailor. Sew your own.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l2.rules", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(watching two housemates argue over the last yoghurt) \"Where I grew up, that gets settled in ten seconds. In here nobody knows the rules, so it takes all day.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What were the rules where you grew up?\"",
                        Lands = "\"Simple ones. You learn a lot from simple ones. Here.\"",
                        Even = "\"Short list. I'll give you the gist.\"",
                        Grates = "\"Long story for a yoghurt. Another time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Ten seconds? Show me. Go and settle it.\"",
                        Lands = "\"Ha. Not my kitchen. But I'll tell you how it'd go.\"",
                        Even = "\"Not my fight. Here's how it'd go, though.\"",
                        Grates = "\"And end up in their fight? No thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"In here the rule is whoever's HoH.\"",
                        Lands = "\"True. Which is why I miss the old rules. Listen.\"",
                        Even = "\"Fair. The old ones were better. Here.\"",
                        Grates = "\"Cynical. Fine. Then you don't need mine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.fridge", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(standing at the open fridge, taking nothing) \"Some nights I forget what I'm doing in here. Then I remember, and I'm up till two.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Then tell me what you remember.\"",
                        Lands = "\"Okay. Not the speech version. This.\"",
                        Even = "\"Part of it. Here.\"",
                        Grates = "\"Not like that. Not as an order.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You remember the fridge is open, for one.\"",
                        Lands = "\"Shut it. All right. Here's the other thing.\"",
                        Even = "\"Shut. Fine. The other thing.\"",
                        Grates = "\"Don't mock the two a.m. fridge. It's sacred.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Careful who you tell that to.\"",
                        Lands = "\"I am being careful. That's why it's you.\"",
                        Even = "\"I know. A piece of it, then.\"",
                        Grates = "\"Right. So I won't tell you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.dusk", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(on the back step at dusk, shoes off) \"A year ago I'd never have done anything like this. Any of it. Ask me what changed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"What changed?\"",
                        Lands = "\"One thing. A big one. Here.\"",
                        Even = "\"A few things. The main one's this.\"",
                        Grates = "\"Too much to say on a step. Another night.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Nothing changed. You were always this. Admit it.\"",
                        Lands = "\"Ha. Maybe. Then here's what let it out.\"",
                        Even = "\"Maybe. Here's what I tell myself.\"",
                        Grates = "\"You don't know who I was. Don't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only if you're sure.\"",
                        Lands = "\"I'm sure. That's the surprising part.\"",
                        Even = "\"Sure enough. Here.\"",
                        Grates = "\"Now I'm not. Forget it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.promise", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(rinsing a mug that's already clean) \"I made myself one promise before I came in. Broke it once already. Not again.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's the promise?\"",
                        Lands = "\"Fair question. Straight answer.\"",
                        Even = "\"Roughly this.\"",
                        Grates = "\"If I tell you, it's half yours. Not yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Everyone breaks those. Why's this one different?\"",
                        Lands = "\"Because I know what breaking it cost. Here.\"",
                        Even = "\"Because of this.\"",
                        Grates = "\"Keep pushing and you'll find out the hard way.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Was it 'no more slop'?\"",
                        Lands = "\"No. That one I broke on day two. The real one's this.\"",
                        Even = "\"Close. Not slop.\"",
                        Grates = "\"It's not a slop thing. Forget I said it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.admit", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(sitting on the bottom stair, elbows on knees) \"Everybody's playing for something. Took me a week in here to admit what mine is.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Admitting it's the hard part. Tell me.\"",
                        Lands = "\"It is. I said it to the wall first. Now you.\"",
                        Even = "\"Okay. Quietly.\"",
                        Grates = "\"I didn't say I'd say it out loud.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Knowing someone's reason tells you how they'll vote.\"",
                        Lands = "\"Maybe. Here's mine anyway.\"",
                        Even = "\"So now you'll know how I vote. Fine.\"",
                        Grates = "\"That's exactly why I'm not telling you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"A week? Took me a day.\"",
                        Lands = "\"Show-off. Fine. Here's mine.\"",
                        Even = "\"Congratulations. Mine, then.\"",
                        Grates = "\"Good for you. Mine can wait.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.daycount", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(chalking another mark on the yard wall) \"Day count keeps going up. Every morning I check I still remember why I came.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"And do you?\"",
                        Lands = "\"I do. Want it?\"",
                        Even = "\"Mostly. Here.\"",
                        Grates = "\"Most days. Today's not a sharing day.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Some mornings I don't.\"",
                        Lands = "\"Then borrow mine for a bit.\"",
                        Even = "\"Happens. Here's what keeps mine.\"",
                        Grates = "\"Then you've got your own problem. I'll keep mine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Writing it on the wall would help.\"",
                        Lands = "\"Ha. They'd paint over it. I'll just tell you.\"",
                        Even = "\"I'd rather tell you than the wall.\"",
                        Grates = "\"They'd paint over it. And me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.laces", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(double-knotting shoes before a competition) \"Before every competition I tell myself one thing. Same thing every time. Want to hear it?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only if you want to say it out loud.\"",
                        Lands = "\"I do. Don't laugh.\"",
                        Even = "\"All right. Same words every time.\"",
                        Grates = "\"Now it sounds silly. Later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is it 'don't fall off'?\"",
                        Lands = "\"That's the second thing. The first is this.\"",
                        Even = "\"That too. The real one's this.\"",
                        Grates = "\"That's not funny ten minutes before a wall comp.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"A pep talk won't hold you on the wall.\"",
                        Lands = "\"No. But it's why I stay up there.\"",
                        Even = "\"It's not a pep talk. It's a reason.\"",
                        Grates = "\"Then go stand on your own wall.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.diary", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(low, just after a camera pans away) \"I'm going to tell you something I haven't said in the diary room. They ask me every day.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why me and not them?\"",
                        Lands = "\"Because you'll listen without editing it.\"",
                        Even = "\"Good question. Because. Here.\"",
                        Grates = "\"If you're asking why, maybe you're not the one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then say it quietly. They pan back.\"",
                        Lands = "\"Right. Quietly. Here.\"",
                        Even = "\"I know the cameras. Here.\"",
                        Grates = "\"I know when they pan back. I live here too.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You don't owe anybody that.\"",
                        Lands = "\"I know. That's why it's you.\"",
                        Even = "\"No. But I'll say it.\"",
                        Grates = "\"Then I'll keep it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.l3.walkout", Pool = TalkPools.Steady, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(bedroom dark, whispering across the gap between beds) \"You awake? I keep thinking about what I'd say if I walked out tomorrow.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Awake. Tell me.\"",
                        Lands = "\"Okay. I've had it ready for days.\"",
                        Even = "\"Short version. Here.\"",
                        Grates = "\"Nah. I'll sleep on it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"You're not walking out. Say what you'd say if you won.\"",
                        Lands = "\"Ha. Same thing, really. Here.\"",
                        Even = "\"Same answer. Mostly.\"",
                        Grates = "\"Don't jinx it. Night.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Say it now, in case.\"",
                        Lands = "\"In case. Yeah. Okay.\"",
                        Even = "\"Fair. Here.\"",
                        Grates = "\"That's grim. Go to sleep.\"",
                    },
                },
            },
        };
    }
}
