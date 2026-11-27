namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, Jamie Roberts's own talks (slice S5, talk rules 5; spec 5.4; ruling R1: the talks are C# tables): a first
    /// talk and layer frames that lead to the next fact on their sheet, where they stand this week, the week's events, and house
    /// life, in their own voice. Every row carries Owner = <see cref="Owner"/> and <see cref="TalkRules.ComesToTalk"/>
    /// (<see cref="TalkRow.FromRules"/>), so no season below talk rules 5 is offered one. A fictional regular (Roster.Regular):
    /// no line names a real person; only the standing lines name a housemate, through their tokens; the event lines carry no
    /// token and no gendered word. Three replies a talk, each with its line for every landing. American spelling.
    /// </summary>
    public static class TalkTableJamieRoberts
    {
        public const string Owner = "jamie-roberts";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "jamie.l1.first", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { First = true },
                Open = "(handing you a plate before you've sat down) \"Eat something. Everybody in here forgets to eat, and then everybody cries by midnight. Sit.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. Sit with me and tell me about you.\"",
                        Lands = "\"Look at you, asking first. Okay. One thing about me.\"",
                        Even = "\"Sure. Eat while I talk. Here.\"",
                        Grates = "\"Eat first. Questions after. That's the rule.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm not hungry. I'd rather talk.\"",
                        Lands = "\"Honest. Good. Eat anyway, and I'll talk. Here.\"",
                        Even = "\"You'll be hungry in an hour. Fine. Here's me.\"",
                        Grates = "\"Everybody says that. Eat.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do you feed everyone, or am I special?\"",
                        Lands = "\"Everyone. But you're the first one who asked. Here's something.\"",
                        Even = "\"Everyone. Don't let it go to your head. Here.\"",
                        Grates = "\"Everyone. And now I'm rethinking you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.l1.night", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(wiping the counter at three a.m., wide awake) \"Night shift brain. My body thinks it's the middle of my workday. Pull up a stool.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll keep you company till you're tired.\"",
                        Lands = "\"That's kind. You'll fade first. Here's something while you last.\"",
                        Even = "\"You'll fade first. Okay. One thing.\"",
                        Grates = "\"You'll be asleep in ten minutes. Go to bed.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's your workday, then?\"",
                        Lands = "\"You actually want to know. Okay. Here.\"",
                        Even = "\"Long. Here's the short version.\"",
                        Grates = "\"Too tired to explain. Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Three a.m. is a good time to talk strategy.\"",
                        Lands = "\"Ha. Maybe for you. Fine. Here's something that isn't strategy.\"",
                        Even = "\"Not strategy. Something else, though.\"",
                        Grates = "\"Everything's strategy with you. Go to bed.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.l1.kit", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(sorting the first-aid kit on the bathroom counter by size) \"Somebody's going to cut a finger in a comp. I like knowing where the bandages are.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You're the most prepared person here.\"",
                        Lands = "\"Old habit. Since you noticed, here's a bit about me.\"",
                        Even = "\"Habit. Here's one thing.\"",
                        Grates = "\"Somebody has to be. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Can I be your assistant? I'll hold the tape.\"",
                        Lands = "\"Hired. Hold the tape. I'll talk while I sort.\"",
                        Even = "\"Fine. Hold it straight. One thing about me.\"",
                        Grates = "\"You'll lose it in a minute. No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You can't take care of everyone in here.\"",
                        Lands = "\"I know. Doesn't stop me. Here's why.\"",
                        Even = "\"I know. Here's something anyway.\"",
                        Grates = "\"Don't tell me what I can do. I hear it enough.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.l1.check", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting down next to you, studying your face) \"How are you doing? Not the house answer. The real one. I can tell the difference.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Honestly? Tired. A little lonely.\"",
                        Lands = "\"Thank you for saying it. Okay. I'll give you a real one back.\"",
                        Even = "\"That's fair. Me too. Here's mine.\"",
                        Grates = "\"You're saying what I want to hear. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Better now that you asked.\"",
                        Lands = "\"Good. That's what I'm here for. One real thing about me, then.\"",
                        Even = "\"Good. My turn. Here.\"",
                        Grates = "\"That's the house answer. Nice try.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'm fine. Really. Let's talk about you.\"",
                        Lands = "\"You're not, but okay. Me, then.\"",
                        Even = "\"Okay. Me, then. Just a little.\"",
                        Grates = "\"Fine is the house answer. I'll wait.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.l2.pasta", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(stirring a pot that doesn't need stirring) \"When I'm stressed, I cook. So you know, this is a lot of pasta.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then I'll stay and help you eat it.\"",
                        Lands = "\"Good. Grab a bowl. And since you stayed, a real thing.\"",
                        Even = "\"Okay. Bowls are in the left cupboard. Here's something.\"",
                        Grates = "\"It's not for you. It's for the stress. Go.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's stressing you?\"",
                        Lands = "\"You ask like you mean it. Okay. Here.\"",
                        Even = "\"Lots. Here's one.\"",
                        Grates = "\"Everything. I'm not getting into it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Stop stirring and talk to me.\"",
                        Lands = "\"Bossy. I can work with bossy. Spoon down. Here.\"",
                        Even = "\"I can stir and talk. Here.\"",
                        Grates = "\"I'll stir if I want to stir.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.l2.toonice", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(scraping a plate a little too hard) \"Someone just told me I'm too nice to win this. I'm in a mood. Ask me something real before it passes.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Okay. Something real. Go.\"",
                        Lands = "\"Okay. Here's real.\"",
                        Even = "\"Fine. Here.\"",
                        Grates = "\"Not like that. Ask nicer.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're not too nice. You're the right amount.\"",
                        Lands = "\"Ha. The right amount. I'll take it. Here's something.\"",
                        Even = "\"Thanks. Okay. One thing.\"",
                        Grates = "\"Don't handle me. Ask me something.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Being underestimated is useful. Lean into it.\"",
                        Lands = "\"Maybe. But here's something they don't know.\"",
                        Even = "\"Maybe. Here's one thing.\"",
                        Grates = "\"Useful. Right. That's what they said too.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.l2.steps", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(on the back steps, knees pulled up) \"At home I'd be on my own steps right now with a coffee, yelling at the neighbors' dog. I miss the yelling.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Tell me about home.\"",
                        Lands = "\"You asked. Okay. Here's a piece of it.\"",
                        Even = "\"A little. Here.\"",
                        Grates = "\"Not tonight. It makes me miss it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You could yell at the camera crew instead.\"",
                        Lands = "\"Don't tempt me. Okay. Here's something from home.\"",
                        Even = "\"Maybe tomorrow. Here's something.\"",
                        Grates = "\"That's not the same and you know it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"We can just sit. You don't have to talk.\"",
                        Lands = "\"That's exactly why I want to. Here.\"",
                        Even = "\"I'll talk a little. Here.\"",
                        Grates = "\"Then why'd you come out here? Go on.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.l3.oneline", Owner = Owner, Kind = TalkKind.Layer, Layer = 3, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 3 },
                Open = "(taking your mug and setting it down, so you have to look up) \"I forgive almost everything in here. Almost. Want to know the one thing I won't?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Yes. Tell me, and I won't ever do it.\"",
                        Lands = "\"Good. I believe you. Here it is.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Everybody promises that. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Only if it's okay to tell me.\"",
                        Lands = "\"It is with you. That's the point. Here.\"",
                        Even = "\"It is. Mostly. Here.\"",
                        Grates = "\"I asked if you wanted to know. Do you?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Tell me, so I know where the line is.\"",
                        Lands = "\"Fine. Here's the line. Don't go near it.\"",
                        Even = "\"Here's the line.\"",
                        Grates = "\"So you can walk right up to it? No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.st.dishes", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(handing you a dish towel, voice low under the running tap) \"Dry. Look busy. You want to know where I am this week. I'll tell you over the dishes.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Only if you're okay telling me.\"",
                        Lands = "\"With you, I am. Here.\"",
                        Even = "\"I am. Here's most of it.\"",
                        Grates = "\"I said I'd tell you. Don't make it a whole thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Tell me straight. I'll keep it.\"",
                        Lands = "\"Straight. Okay. Here.\"",
                        Even = "\"Okay. The quick version.\"",
                        Grates = "\"You'll get the quick version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Give me names. I'll do the math.\"",
                        Lands = "\"The math. Sure. Here.\"",
                        Even = "\"Names, then. Some.\"",
                        Grates = "\"I'm not your spreadsheet. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"{close} is the one I check on first. {worry} is the one I watch. Me? {safe}. Keep drying.\"",
                    Neutral = "\"There's someone I check on first. Someone I watch. Me? {safe}. Keep drying.\"",
                    Dishonest = "\"{close} is the one I check on first. {worry} is the one I watch. Me? I'm all right. Keep drying.\"",
                },
            },
            new TalkRow
            {
                Id = "jamie.st.tea", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(on the back steps with two mugs, holding one out) \"Sit. You want my honest read on this week, and you're getting it, because you asked nicely.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I did ask nicely. Go on.\"",
                        Lands = "\"You did. Okay. All of it.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Not that nicely. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What if I hadn't asked nicely?\"",
                        Lands = "\"Then you'd get the tea and nothing else. Here.\"",
                        Even = "\"Then less. Here.\"",
                        Grates = "\"Then this. The short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Don't soften it. Give it to me exactly.\"",
                        Lands = "\"Fine. Exactly. Here.\"",
                        Even = "\"Okay. As exact as I get.\"",
                        Grates = "\"Don't tell me how to say it. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"If I'm going to the end with anybody, it's {close}. {worry} I don't trust. Me? {safe}. Drink your tea.\"",
                    Neutral = "\"I know who I'm going to the end with. I know who I don't trust. Me? {safe}. Drink your tea.\"",
                    Dishonest = "\"If I'm going to the end with anybody, it's {close}. {worry} I don't trust. Me? Fine. Drink your tea.\"",
                },
            },
            new TalkRow
            {
                Id = "jamie.event.player-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerHoH, Window = TalkWindows.AfterHoH },
                Open = "(coming up the stairs with a plate under foil) \"Congratulations. I brought food, because you won't eat today. Everybody forgets to eat when they win.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. Stay and eat some with me.\"",
                        Lands = "\"Five minutes. Then the line forms. Eat the big piece.\"",
                        Even = "\"A few minutes. Eat.\"",
                        Grates = "\"Just eat. I'm not here to visit.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know you're not here to campaign. Thank you.\"",
                        Lands = "\"I'm not. I'll campaign tomorrow, like a normal person.\"",
                        Even = "\"I'm not. Eat.\"",
                        Grates = "\"Well, now it feels like I am.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What does the plate cost me?\"",
                        Lands = "\"Ha. Nothing. Fair question, though.\"",
                        Even = "\"Nothing. It's food.\"",
                        Grates = "\"It's food. Not a deal. Wow.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.event.npc-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcHoH, Window = TalkWindows.AfterHoH },
                Open = "(sitting on the HoH bed, turning the key over) \"I won. Me. Now I have to make a move that's mine, and everybody's already telling me what it should be.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Then make your own. Not anyone else's.\"",
                        Lands = "\"That's what I needed to hear. Thank you.\"",
                        Even = "\"That's the plan. It's harder than it sounds.\"",
                        Grates = "\"Easy for you to say.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You've earned this. Take a breath first.\"",
                        Lands = "\"Okay. Breathing. You're right.\"",
                        Even = "\"I'm breathing. Barely.\"",
                        Grates = "\"I don't need a breath. I need a plan.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do I get a bigger plate now?\"",
                        Lands = "\"You get the whole pan. Don't tell anyone.\"",
                        Even = "\"Same plate. HoH doesn't change the portions.\"",
                        Grates = "\"Is that all you came up here for?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.event.player-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerNominated, Window = TalkWindows.AfterNoms },
                Open = "(sitting on the edge of your bed with a glass of water) \"Drink this. You're on the block, and I'm not letting you do it thirsty.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. I needed someone right now.\"",
                        Lands = "\"I know. I'm here. Drink.\"",
                        Even = "\"Drink. Then talk.\"",
                        Grates = "\"Drink first. Feelings after.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Where are the votes?\"",
                        Lands = "\"Closer than you think. I'll count with you.\"",
                        Even = "\"Not sure yet. I'll find out.\"",
                        Grates = "\"Water first. Votes later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Don't fuss. I'm fine.\"",
                        Lands = "\"You're not. But fine. Drink anyway.\"",
                        Even = "\"Okay. Drink anyway.\"",
                        Grates = "\"Fussing is what I do. Drink the water.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.event.npc-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcNominated, Window = TalkWindows.AfterNoms },
                Open = "(scrubbing a pan that is already clean) \"I'm on the block. I'm fine. I'm totally fine. I'm going to scrub this pan until it's a mirror.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Put the pan down. Come here.\"",
                        Lands = "\"Okay. Okay. Just for a second.\"",
                        Even = "\"One more scrub. Then okay.\"",
                        Grates = "\"If I stop scrubbing I'll cry. Let me scrub.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You've got more votes than you think.\"",
                        Lands = "\"Say that again. Slower.\"",
                        Even = "\"Maybe. Count them for me?\"",
                        Grates = "\"You don't know that. Nobody knows that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's figure out who we need to flip.\"",
                        Lands = "\"Okay. Yes. Plan first, panic later.\"",
                        Even = "\"Later. Let me finish the pan.\"",
                        Grates = "\"Give me five minutes before you make me a project.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.event.npc-veto", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcVeto, Window = TalkWindows.AfterVeto },
                Open = "(holding the veto, eyes a little wet) \"I won the veto. Me. The meeting's over and I'm still shaking. I'm going to wear this thing to bed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You deserve it. Wear it everywhere.\"",
                        Lands = "\"I will. In the shower, even.\"",
                        Even = "\"I'll think about it.\"",
                        Grates = "\"It's a medal, not a costume. But thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You made that call yourself. That counts.\"",
                        Lands = "\"It does, doesn't it. My call. Nobody else's.\"",
                        Even = "\"It does. I think.\"",
                        Grates = "\"Don't make it a speech. Let me enjoy it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Next week, win the HoH too.\"",
                        Lands = "\"Ha. One medal at a time. But yes.\"",
                        Even = "\"Maybe. Let me have tonight.\"",
                        Grates = "\"Don't push me into next week already.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.event.ally-evicted", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.AllyEvicted, Window = TalkWindows.AfterEviction },
                Open = "(folding a sweater the evicted houseguest left behind) \"They forgot this. I said I'd look out for them in here, and I couldn't.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You did look out for them. The vote wasn't yours.\"",
                        Lands = "\"Thank you. I needed somebody to say that.\"",
                        Even = "\"I know. It still stings.\"",
                        Grates = "\"It doesn't feel that way.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's the game. You can't save everyone.\"",
                        Lands = "\"I know. I hate that you're right.\"",
                        Even = "\"I know.\"",
                        Grates = "\"I know it's the game. I didn't ask.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(help fold it)",
                        Lands = "\"Thank you. Corners together. There.\"",
                        Even = "\"Okay. There.\"",
                        Grates = "\"I can fold it. I fold things all day.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.event.couple-noticed", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.CoupleNoticed },
                Open = "(topping up your coffee without asking) \"So the whole house knows about you two. I'm happy for you. I'm also worried, so drink this.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. That means a lot.\"",
                        Lands = "\"Good. Be careful in there. Both of you.\"",
                        Even = "\"Sure. Just be careful.\"",
                        Grates = "\"Don't thank me. Be careful.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Are we a target now?\"",
                        Lands = "\"Yes. A pair always is. Keep your friends close this week.\"",
                        Even = "\"Probably. Watch the votes.\"",
                        Grates = "\"You knew that going in.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Worried? You?\"",
                        Lands = "\"Me. Always. It's a whole job.\"",
                        Even = "\"Always. Drink.\"",
                        Grates = "\"Laugh now. I'll be right later.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.cheese", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(eyeing the HoH basket on its way upstairs) \"Every week that basket goes up, and every week I'd trade my vote for one of those little cheeses. Kidding. Mostly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll smuggle you one. For a price.\"",
                        Lands = "\"Name your price. I'm weak.\"",
                        Even = "\"Ha. Maybe.\"",
                        Grates = "\"Never mind. Forget I said it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Next week it's yours. I can feel it.\"",
                        Lands = "\"From your mouth. Thank you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Don't jinx it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You wouldn't trade your vote for a cheese.\"",
                        Lands = "\"No. But I'd think about it. Hard.\"",
                        Even = "\"No. Probably not.\"",
                        Grates = "\"It was a joke.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.dinner", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(setting out dinner plates, counting them twice) \"Nomination night. Nobody eats. I make dinner anyway. They'll come down at ten and eat it cold.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll eat it now, while it's warm.\"",
                        Lands = "\"That's my favorite person tonight. Sit.\"",
                        Even = "\"Good. Sit.\"",
                        Grates = "\"Don't do it for me. Do it because you're hungry.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Leave it out. They'll find it.\"",
                        Lands = "\"That's what I always do. Foil on top.\"",
                        Even = "\"Yeah. Foil.\"",
                        Grates = "\"Cold pasta is a crime.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's nice that you still do it.\"",
                        Lands = "\"Somebody has to. It might as well be the one who likes it.\"",
                        Even = "\"Habit.\"",
                        Grates = "\"Don't make it sound sad.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.ice", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(filling a bag with ice for someone's sore shoulder) \"Veto comp casualties, round two. Hold the bag. Ice goes on for twenty minutes, not five.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're good at this.\"",
                        Lands = "\"Years of practice. Hold it still.\"",
                        Even = "\"Thanks. Still.\"",
                        Grates = "\"Hold it, don't compliment it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do I get a bag? I watched the whole comp. Very tiring.\"",
                        Lands = "\"Ha. You get a cookie. That's my best offer.\"",
                        Even = "\"No. Hold this one.\"",
                        Grates = "\"Not funny. Somebody's hurt.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Twenty minutes. Got it.\"",
                        Lands = "\"Good. The timer's on the stove.\"",
                        Even = "\"Good.\"",
                        Grates = "\"I'll watch the clock myself.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.sheets", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(stripping the sheets off the bed that just emptied) \"I always make up the bed after. It feels wrong to leave it. Help me with the corners?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(take the other corner)",
                        Lands = "\"Thank you. You're good at corners.\"",
                        Even = "\"There. Done.\"",
                        Grates = "\"Other way. Never mind, I've got it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Of course. You don't have to do this alone.\"",
                        Lands = "\"I know. It's nicer with two.\"",
                        Even = "\"Thanks.\"",
                        Grates = "\"It's a bed. Don't make it a moment.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Leave it. Let production do it.\"",
                        Lands = "\"Ha. They'd do it wrong. Corners.\"",
                        Even = "\"No. Corners.\"",
                        Grates = "\"I said I always do it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.u.eaten", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(pointing a wooden spoon at you) \"Have you eaten? Don't lie. I'll know.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Not yet. Honestly.\"",
                        Lands = "\"Thank you for not lying. There's a plate in the oven.\"",
                        Even = "\"Plate's in the oven.\"",
                        Grates = "\"I knew it. Oven. Go.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Define eaten.\"",
                        Lands = "\"Ha. Crackers don't count. Oven. Now.\"",
                        Even = "\"Crackers don't count.\"",
                        Grates = "\"Don't be smart. Eat.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(raise both hands and head for the kitchen)",
                        Lands = "\"Good. Second shelf.\"",
                        Even = "\"Good.\"",
                        Grates = "\"And a vegetable.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.u.scale", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting down beside you with a sigh) \"Okay. Quick check. Scale of one to ten, how are we doing?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"A six. Honestly.\"",
                        Lands = "\"A six is honest. I'll take a six.\"",
                        Even = "\"Okay. Six. Noted.\"",
                        Grates = "\"That's a four pretending. Fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Better now. Seven.\"",
                        Lands = "\"Seven. Good. Mine went up too.\"",
                        Even = "\"Seven. Okay.\"",
                        Grates = "\"You're just saying that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Eleven. Obviously.\"",
                        Lands = "\"Liar. But okay. I'll take the energy.\"",
                        Even = "\"Sure. Eleven.\"",
                        Grates = "\"Don't. I asked for real.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.kettle", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 3 },
                Open = "(rereading a note someone left on the fridge) \"A few weeks in and I've started missing silly things. The sound of my own kettle. My own couch.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"What else do you miss?\"",
                        Lands = "\"My own pillow. Real coffee. Quiet. You?\"",
                        Even = "\"Little things. Lots of them.\"",
                        Grates = "\"Don't get me started.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It means you've settled in. That's good.\"",
                        Lands = "\"Huh. I never thought of it that way.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It means I'm homesick. That's all.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You miss a kettle more than people?\"",
                        Lands = "\"The kettle never voted against me.\"",
                        Even = "\"Some days.\"",
                        Grates = "\"That's not what I said.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.shortlist", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 6 },
                Open = "(sitting on the counter, which is against house rules) \"Every week the house gets smaller. I keep thinking about who I'd want next to me at the end. It's a short list.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Am I on it?\"",
                        Lands = "\"You might be. Keep being honest with me.\"",
                        Even = "\"Ask me next week.\"",
                        Grates = "\"Don't fish. It's beneath you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Whoever's on it is lucky.\"",
                        Lands = "\"That's a nice thing to say. Thank you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Flattery. Noted.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What does someone need to get on it?\"",
                        Lands = "\"Ha. Honesty. Then it's easy.\"",
                        Even = "\"Honesty, mostly.\"",
                        Grates = "\"If you have to ask how, you're not.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.scrubs", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(in an old pair of scrub pants, the only pants that fit right) \"These are the comfiest pants in the house. They've survived nights you wouldn't believe.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"They look like they've survived a war.\"",
                        Lands = "\"They have. Several. Very loyal pants.\"",
                        Even = "\"They have. Leave them alone.\"",
                        Grates = "\"Watch it. These pants have seen things.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"They suit you.\"",
                        Lands = "\"Thank you. Finally, somebody gets it.\"",
                        Even = "\"They're pants. But thanks.\"",
                        Grates = "\"They don't suit anyone. That's not the point.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Comfort beats style in here.\"",
                        Lands = "\"Every time. You get it.\"",
                        Even = "\"Every time.\"",
                        Grates = "\"It's not either one. It's both.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.pantry", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(yawning at four in the afternoon) \"I still can't sleep at night. My body thinks I'm on shift. I've reorganized the pantry twice.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Want me to sit up with you tonight?\"",
                        Lands = "\"You'd fall asleep in ten minutes. But that's nice.\"",
                        Even = "\"Maybe. Just for a bit.\"",
                        Grates = "\"No. One of us should sleep.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Skip the nap. Force your body to reset.\"",
                        Lands = "\"You sound like my charge nurse. Fine. No nap.\"",
                        Even = "\"I'll try. I'll fail.\"",
                        Grates = "\"That's not how it works. Trust me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So the pantry's yours now?\"",
                        Lands = "\"It's alphabetical. Don't touch it.\"",
                        Even = "\"It's organized. Don't move anything.\"",
                        Grates = "\"Don't mess it up. I mean it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.boston", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(at the window, arms crossed) \"It's too warm here. Back in Boston it'd be freezing right now, and I'd be complaining about it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you miss the cold?\"",
                        Lands = "\"I miss complaining about the cold. Different thing.\"",
                        Even = "\"A little.\"",
                        Grates = "\"Nobody misses the cold. That's not the point.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You'd complain about any weather.\"",
                        Lands = "\"Ha. True. It's a Boston thing.\"",
                        Even = "\"Probably.\"",
                        Grates = "\"Not any weather. This weather.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"We'll find you something else to complain about.\"",
                        Lands = "\"Oh, plenty. The dishwasher, for one.\"",
                        Even = "\"Easy.\"",
                        Grates = "\"Don't humor me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.rinse", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(catching a dirty plate before it reaches the pile in the sink) \"Rule of the kitchen: you rinse it, you stack it. It's not hard. It's one rule.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I always rinse mine.\"",
                        Lands = "\"I know. I've seen you. Thank you.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Not always. I've been watching.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Who broke the rule this time?\"",
                        Lands = "\"I'm not naming names. It was the yard crowd.\"",
                        Even = "\"I'm not saying.\"",
                        Grates = "\"Don't make me the bad guy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Then call them out. Loudly.\"",
                        Lands = "\"Maybe I will. At dinner. With the plate.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"I'm not the house enforcer. Don't.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jamie.rh.recipes", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(writing on the back of a cereal box) \"I'm writing down recipes from in here. Slop casserole. It's not good. It's edible.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Put it in a cookbook. Survival Recipes, Volume One.\"",
                        Lands = "\"Volume One. Yes. You're writing the foreword.\"",
                        Even = "\"Maybe. If I win.\"",
                        Grates = "\"Nobody would buy it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"If you made it, it's good.\"",
                        Lands = "\"It's not. But thank you.\"",
                        Even = "\"It's edible. That's my ceiling.\"",
                        Grates = "\"You haven't tried it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Edible counts in here.\"",
                        Lands = "\"That's what I keep telling everybody.\"",
                        Even = "\"It counts.\"",
                        Grates = "\"Edible is a low bar.\"",
                    },
                },
            },
        };
    }
}
