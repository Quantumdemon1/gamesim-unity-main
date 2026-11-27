namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, Quinn Martinez's own talks (slice S5, talk rules 5; spec 5.4; ruling R1: the talks are C# tables): a first
    /// talk and layer frames that lead to the next fact on their sheet, where they stand this week, the week's events, and house
    /// life, in their own voice. Every row carries Owner = <see cref="Owner"/> and <see cref="TalkRules.ComesToTalk"/>
    /// (<see cref="TalkRow.FromRules"/>), so no season below talk rules 5 is offered one. A fictional regular (Roster.Regular):
    /// no line names a real person; only the standing lines name a housemate, through their tokens; the event lines carry no
    /// token and no gendered word. Three replies a talk, each with its line for every landing. American spelling.
    /// </summary>
    public static class TalkTableQuinnMartinez
    {
        public const string Owner = "quinn-martinez";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "quinn.l1.first", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { First = true },
                Open = "(perched on the arm of the couch, angled toward the nearest camera) \"Okay, first impression. You get one question. Make it good. The feeds are watching.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Why are you angled at the camera?\"",
                        Lands = "\"Because it's my good side. See? Good question. One thing about me.\"",
                        Even = "\"Habit. Okay. Here's something.\"",
                        Grates = "\"Boring question. Try again tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"One question? I was promised three.\"",
                        Lands = "\"Ha. Okay, I like you. One question, one answer. Here.\"",
                        Even = "\"One. Don't get greedy. Here.\"",
                        Grates = "\"Funny. No. Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No pressure. Tell me whatever you want.\"",
                        Lands = "\"Whatever I want. Dangerous. Here.\"",
                        Even = "\"Okay. Something. Here.\"",
                        Grates = "\"That's the most boring thing anyone's said to me today.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.l1.lighting", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(squinting up at the bathroom lights) \"This lighting is a crime. Nobody looks good under it. I'm filing a complaint with the cameras.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You'd look good under a flashlight.\"",
                        Lands = "\"Correct. And you're smart. One fact for the smart one.\"",
                        Even = "\"Obviously. Here's something.\"",
                        Grates = "\"Flattery is lazy. Try harder.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"The lighting's fine. You're just used to better.\"",
                        Lands = "\"Finally, somebody honest. Here. Something real.\"",
                        Even = "\"I'm used to perfect. Here.\"",
                        Grates = "\"Fine? Fine? Leave.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who are you really performing for in here?\"",
                        Lands = "\"Ooh. Everyone. Here's a little of why.\"",
                        Even = "\"Everyone. Here.\"",
                        Grates = "\"That's a question for a camera, not for me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.l1.voiceover", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(narrating at the counter in a low voice-over) \"And then our hero made a sandwich, and the whole house was shook. Sorry. I narrate. Ask me something.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Narrate me something true, then.\"",
                        Lands = "\"Ooh, a director. Okay. Something true. Here.\"",
                        Even = "\"Fine. True. Here.\"",
                        Grates = "\"No notes from you. Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"And then the house rolled its eyes.\"",
                        Lands = "\"Ha. Okay, you're good at this. Here's one for you.\"",
                        Even = "\"Rude. Accurate. Here.\"",
                        Grates = "\"That's not the edit I'd pick.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll just watch the sandwich.\"",
                        Lands = "\"A fan. Okay. One thing while it gets made.\"",
                        Even = "\"Okay. One thing.\"",
                        Grates = "\"Watching is boring. Say something.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.l1.face", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(pointing a spoon at you across the kitchen) \"You. Always watching, never talking. Say something to my face. I'll answer to yours.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Fine. You're louder than you need to be.\"",
                        Lands = "\"Ha. Thank you. Finally. Here's something for being brave.\"",
                        Even = "\"Noted. Here's something.\"",
                        Grates = "\"That's all you've got? Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Okay. Tell me one true thing.\"",
                        Lands = "\"To your face. Okay. Here.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Too easy. Ask me something harder.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"I've been watching to see who's real in here.\"",
                        Lands = "\"Smart. Okay. Here's me being real.\"",
                        Even = "\"Sure. Here's one real thing.\"",
                        Grates = "\"Watching is what the fake ones do.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.l2.hood", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(curled on the couch, hood up, the only time the hood is ever up) \"Off-camera me is different. Not a lot. A little. You want the little?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I want whatever you'll give me.\"",
                        Lands = "\"That's the right answer. Here's the little.\"",
                        Even = "\"Okay. A little. Here.\"",
                        Grates = "\"Too soft. Ask me again with a spine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I want the whole thing, not the little.\"",
                        Lands = "\"Greedy. I respect that. Here.\"",
                        Even = "\"You get the little. Here.\"",
                        Grates = "\"You get nothing for that. Nice try.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Why are you offering?\"",
                        Lands = "\"Because you're the only one who hasn't asked. Here.\"",
                        Even = "\"Because I'm bored. Here.\"",
                        Grates = "\"Forget it. You ruined it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.l2.scroll", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(thumb moving on nothing, out of habit) \"No phone in here, and my thumb still scrolls. It's a reflex. Ask me what I'd be posting right now.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What would you be posting right now?\"",
                        Lands = "\"A clip of you asking that. Kidding. Here's something real.\"",
                        Even = "\"Nothing good. Here's something.\"",
                        Grates = "\"Nothing for you. Later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you ever like being offline?\"",
                        Lands = "\"Huh. Nobody asks that. Okay. Here.\"",
                        Even = "\"Sometimes. Here.\"",
                        Grates = "\"I've never been offline. Next.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Maybe it's good to take a break.\"",
                        Lands = "\"Maybe. Don't tell my followers. Here.\"",
                        Even = "\"Maybe. Here.\"",
                        Grates = "\"Don't tell me to relax. Seriously.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.l2.story", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(dragging two chairs together in the yard) \"Everyone in here has a story about me. Mine's better. Want a piece of it before somebody tells it wrong?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Yes. Before anyone else gets to.\"",
                        Lands = "\"That's the energy. Here.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Don't rush the story. Later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd rather hear it from you anyway.\"",
                        Lands = "\"Good answer. Here's a piece.\"",
                        Even = "\"Sure. A piece.\"",
                        Grates = "\"Everyone says that. Later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What's the angle?\"",
                        Lands = "\"The angle is it's true. Here.\"",
                        Even = "\"No angle. Here.\"",
                        Grates = "\"Wow. Everything's an angle to you. No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.l3.offstage", Owner = Owner, Kind = TalkKind.Layer, Layer = 3, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 3 },
                Open = "(on the floor of the empty living room, no camera angle at all) \"No performance. Ask me the thing nobody's allowed to do to me. I'll tell you. Once.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's the thing?\"",
                        Lands = "\"Okay. Here it is. Don't use it.\"",
                        Even = "\"Here.\"",
                        Grates = "\"Ask like it matters. Not tonight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll keep it safe. Whatever it is.\"",
                        Lands = "\"I believe you. That's weird for me. Here.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Don't be soft about it. Forget it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You don't have to perform for me either.\"",
                        Lands = "\"I'm not. That's the point. Here.\"",
                        Even = "\"I know. Here.\"",
                        Grates = "\"Don't tell me what I'm doing. Never mind.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.st.board", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(on the couch, facing the camera, talking out of the side of the mouth) \"You want the real board? Fine. Lean in. If you repeat it, I'll know.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Lean in. Go.\"",
                        Lands = "\"Okay. The whole board. Here.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Pushy. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Are you going to look at the camera the whole time?\"",
                        Lands = "\"Yes. It's my good side. Here.\"",
                        Even = "\"Yes. Here.\"",
                        Grates = "\"I'm looking at the camera because you're being annoying. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"I won't repeat it. Names?\"",
                        Lands = "\"Names. Okay.\"",
                        Even = "\"Some names.\"",
                        Grates = "\"Ugh. You'd repeat it. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"{close} is my ride to the end. {worry} is the plot twist I didn't write. Me? {safe}. That's the episode.\"",
                    Neutral = "\"I've got a ride to the end. Someone's a plot twist I didn't write. Me? {safe}. That's all you get.\"",
                    Dishonest = "\"{close} is my ride to the end. {worry} is the plot twist I didn't write. Me? Untouchable. That's the episode.\"",
                },
            },
            new TalkRow
            {
                Id = "quinn.st.lounger", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(stretched on a lounger in the yard, sunglasses on) \"Everyone's asking where I stand. I tell them all something different. You want the true one?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"The true one. Please.\"",
                        Lands = "\"Okay. You, I'll give it to. Here.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Please. So polite. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Tell me the true one or I'll assume the worst.\"",
                        Lands = "\"Ooh. Finally someone with nerve. Here.\"",
                        Even = "\"Fine. Here.\"",
                        Grates = "\"Assume away. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Whatever you want to tell me is fine.\"",
                        Lands = "\"Fine. Then I want to tell you the true one.\"",
                        Even = "\"Then here's something.\"",
                        Grates = "\"Fine is boring. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"I'd keep {close} over anyone. {worry} is coming for me, I can feel it. Me? {safe}. Don't make it weird.\"",
                    Neutral = "\"There's someone I'd keep over anyone. Someone's coming for me. Me? {safe}. Don't make it weird.\"",
                    Dishonest = "\"I'd keep {close} over anyone. {worry} is coming for me, I can feel it. Me? Totally fine. Don't make it weird.\"",
                },
            },
            new TalkRow
            {
                Id = "quinn.event.player-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerHoH, Window = TalkWindows.AfterHoH },
                Open = "(leaning in the HoH doorway, arms folded) \"Look at you, up here with the good lighting. Everybody downstairs is rehearsing a pitch. I didn't.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"So what's your pitch?\"",
                        Lands = "\"No pitch. Just this: put me up and it's a great episode. Don't.\"",
                        Even = "\"No pitch. Just vibes.\"",
                        Grates = "\"I said I didn't rehearse. Keep up.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You definitely rehearsed in the shower.\"",
                        Lands = "\"Twice. Okay, three times. It was a great pitch.\"",
                        Even = "\"Maybe once.\"",
                        Grates = "\"Rude. Accurate. Rude.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What do you want out of this week?\"",
                        Lands = "\"To not be on the block. And a front-row seat.\"",
                        Even = "\"Safety. And snacks.\"",
                        Grates = "\"Ugh. Business talk. Fine. Safety.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.event.npc-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcHoH, Window = TalkWindows.AfterHoH },
                Open = "(spinning slowly in the HoH chair) \"Head of Household. The whole house has to come to me now. This is the best day of my life.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Make it a big week. Don't play it safe.\"",
                        Lands = "\"Safe? Me? Never. Watch this.\"",
                        Even = "\"That's the plan.\"",
                        Grates = "\"I wasn't going to. Obviously.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Don't let it go to your head.\"",
                        Lands = "\"Too late. It's in there. It lives there now.\"",
                        Even = "\"Little late for that.\"",
                        Grates = "\"It's my week. Let me have it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take it slow. You've got all week.\"",
                        Lands = "\"Slow is boring. But fine. One day.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't tell me to slow down. Never tell me that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.event.player-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerNominated, Window = TalkWindows.AfterNoms },
                Open = "(dropping onto the couch next to you) \"You're on the block, and you didn't even make a scene. I'm a little disappointed. What's the plan?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"The plan is you tell me who wanted me up there.\"",
                        Lands = "\"Now we're talking. I'll tell you what I heard.\"",
                        Even = "\"Maybe. I'll find out.\"",
                        Grates = "\"I don't snitch for free.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I need votes. Can I count on yours?\"",
                        Lands = "\"Asked to my face. Yes. For now.\"",
                        Even = "\"Ask me after the veto.\"",
                        Grates = "\"Ask me when you've got a plan.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No scene. I'm staying calm.\"",
                        Lands = "\"Calm is a choice. A boring one, but a choice.\"",
                        Even = "\"Okay. Your call.\"",
                        Grates = "\"Calm is how you go home quietly.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.event.npc-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcNominated, Window = TalkWindows.AfterNoms },
                Open = "(pacing in front of the memory wall) \"They put me up. Me. They want a quiet house. They are not getting one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then fight. Loud.\"",
                        Lands = "\"Loud is my only setting. Watch.\"",
                        Even = "\"Obviously.\"",
                        Grates = "\"I don't need a coach.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You've got more people than you think.\"",
                        Lands = "\"Do I? Name one. Actually, don't. I believe you.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't go soft on me right now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take a breath before you go in there.\"",
                        Lands = "\"One breath. Then I go in.\"",
                        Even = "\"Fine. One.\"",
                        Grates = "\"Do not tell me to breathe. Do not.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.event.npc-veto", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcVeto, Window = TalkWindows.AfterVeto },
                Open = "(wearing the veto over a hoodie like jewelry) \"I won the veto, and it's an accessory now. The whole house is being extremely nice to me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"It clashes with the hoodie.\"",
                        Lands = "\"It clashes with everything. That's the point.\"",
                        Even = "\"It goes with power.\"",
                        Grates = "\"Nothing clashes when you've won.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Did the meeting go how you wanted?\"",
                        Lands = "\"Every eye on me for ten minutes. Yes. Exactly how I wanted.\"",
                        Even = "\"Mostly. The faces were great.\"",
                        Grates = "\"Like I'd give you the recap for free.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Everyone being nice is how you find the snakes.\"",
                        Lands = "\"Ooh. Yes. I'm keeping a list.\"",
                        Even = "\"I know. I'm watching.\"",
                        Grates = "\"I know who the snakes are. Thanks.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.event.ally-evicted", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.AllyEvicted, Window = TalkWindows.AfterEviction },
                Open = "(on the stairs, unusually quiet) \"They were my person. The house took them out on purpose. Fine. Now it's personal.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm sorry. I know they mattered to you.\"",
                        Lands = "\"They did. Don't tell anyone I said that.\"",
                        Even = "\"Yeah.\"",
                        Grates = "\"Don't. I'll cry, and not on my good side.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then make the house pay for it.\"",
                        Lands = "\"Oh, I plan to. Very publicly.\"",
                        Even = "\"I will.\"",
                        Grates = "\"I don't need you to tell me that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Give it a night before you do anything.\"",
                        Lands = "\"One night. Then the gloves come off.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"A night is how people get away with it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.event.couple-noticed", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.CoupleNoticed },
                Open = "(fanning with a magazine) \"You two are the biggest story in the house now. I'm not even mad. It's good content.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Jealous it's not you?\"",
                        Lands = "\"A little. I'd have made it a better story.\"",
                        Even = "\"Of the attention? Slightly.\"",
                        Grates = "\"Of the attention. Not of you two.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"We're a target now, aren't we?\"",
                        Lands = "\"Huge. But big stories last longer in here. Use it.\"",
                        Even = "\"Obviously.\"",
                        Grates = "\"You're asking me? I'm the one gossiping about you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Don't turn us into your storyline.\"",
                        Lands = "\"Fair. Said to my face. Respect.\"",
                        Even = "\"No promises.\"",
                        Grates = "\"It's already a storyline. I'm just narrating.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.selfies", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(staring up at the HoH room windows) \"The HoH room has the only good lighting in the house. Whoever wins it, I'm visiting for the mirror alone.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You'd visit a rival just for a mirror?\"",
                        Lands = "\"For that mirror? Yes. Have you seen it?\"",
                        Even = "\"It's a very good mirror.\"",
                        Grates = "\"Let me dream.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Win it yourself. Then you don't have to visit.\"",
                        Lands = "\"Ooh. Bossy. Noted, and yes.\"",
                        Even = "\"That's the plan.\"",
                        Grates = "\"I'll get there when I get there.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You'd go up there for the lighting, not the power?\"",
                        Lands = "\"Both. Power looks better in good lighting.\"",
                        Even = "\"Mostly the lighting.\"",
                        Grates = "\"Obviously the power. Keep up.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.faces", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(replaying the ceremony with exaggerated faces) \"Did you see the nominee faces? I'm doing impressions. I'm too good at this. It's a problem.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do mine. I dare you.\"",
                        Lands = "(pulling a frozen, startled face) \"That's you. Nailed it.\"",
                        Even = "\"Later. I need to study you more.\"",
                        Grates = "\"You don't have a face worth doing yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"That's a little mean.\"",
                        Lands = "\"It is. You're right. I'll do it nicer.\"",
                        Even = "\"A little. It's still funny.\"",
                        Grates = "\"It's a game show, not a funeral.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Your impressions are dangerous. Keep going.\"",
                        Lands = "\"Finally, an audience. Okay, this one's the HoH.\"",
                        Even = "\"I will.\"",
                        Grates = "\"I don't need encouragement. I need a bigger audience.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.recap", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(holding a spoon like a microphone) \"Veto recap, live from the kitchen. Highlights: two people cried, one lied, and I looked great.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Who lied? Exclusive, please.\"",
                        Lands = "\"Exclusive for you. Not yet. Tune in tomorrow.\"",
                        Even = "\"That's next week's episode.\"",
                        Grates = "\"Exclusives cost more than that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You didn't look that great.\"",
                        Lands = "\"Ha. Brave. Honest. Wrong, but brave.\"",
                        Even = "\"I looked fine. Fine is still great.\"",
                        Grates = "\"Excuse me? Leave my kitchen.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who cried, and why?\"",
                        Lands = "\"That's a real question. I'll tell you later. Off camera.\"",
                        Even = "\"Everyone. Everything.\"",
                        Grates = "\"I'm doing a bit. Don't make it a briefing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.walk", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(checking hair in the reflection of the oven door) \"Eviction night is the best television. Somebody's whole story ends in one walk. Terrifying. Iconic.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's a lot more terrifying when it's you.\"",
                        Lands = "\"True. That's why I'm never walking out that door.\"",
                        Even = "\"That's why I'm not going.\"",
                        Grates = "\"I know that. I live here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You've already planned your exit walk.\"",
                        Lands = "\"Planned and rehearsed. It has a spin.\"",
                        Even = "\"Maybe a little.\"",
                        Grates = "\"I'm not leaving, so no.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Whoever leaves, at least they had a good last week with us.\"",
                        Lands = "\"That's actually nice. Gross. But nice.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"That's a greeting card. Try harder.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.u.entertain", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(glancing up from the couch) \"Oh, you're back. Good. I was getting bored. Entertain me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'm the entertainment now?\"",
                        Lands = "\"You're a supporting role. Promotion pending.\"",
                        Even = "\"Sure. Go.\"",
                        Grates = "\"Not yet. Keep trying.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"No. You entertain me.\"",
                        Lands = "\"Ha. Fine. I'll find something.\"",
                        Even = "\"Bold. Okay.\"",
                        Grates = "\"I'm off duty.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit down and wait)",
                        Lands = "\"Silence. A bold choice. I'll allow it.\"",
                        Even = "\"Okay. We'll sit.\"",
                        Grates = "\"Boring. So boring.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.u.filler", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(waving at a camera in the corner) \"Hi, everyone at home. Nothing's happening. This is filler. Say hi.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "(wave at the camera too)",
                        Lands = "\"Look at you. A natural. They're obsessed with you.\"",
                        Even = "\"Okay, they saw. Good.\"",
                        Grates = "\"Too much. Way too much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm not waving at a camera.\"",
                        Lands = "\"Fair. Mysterious. They'll eat that up.\"",
                        Even = "\"Okay. Suit yourself.\"",
                        Grates = "\"Fine. Be boring on national television.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Filler with you isn't filler.\"",
                        Lands = "\"Ha. That's going in the edit.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"That's a lot. Dial it back.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.myedit", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 3 },
                Open = "(on the couch, feet up) \"A few weeks in. I'd give anything to know my edit. Hero, villain, or comic relief?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Villain. But a fun one.\"",
                        Lands = "\"Yes. That's the correct answer. The fun villain goes far.\"",
                        Even = "\"I'll take it.\"",
                        Grates = "\"Villain? I'm the hero. Obviously.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Comic relief. Definitely.\"",
                        Lands = "\"Rude. But comic relief always makes the finale.\"",
                        Even = "\"Hm. Maybe.\"",
                        Grates = "\"Comic relief goes home early. Take it back.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Depends what you do this week.\"",
                        Lands = "\"Ooh. Strategic. Okay, producer. Noted.\"",
                        Even = "\"Everything depends on this week.\"",
                        Grates = "\"Everything depends on everything. Boring answer.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.pose", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 6 },
                Open = "(practicing a pose on the stairs) \"My finale outfit is planned. It's been planned since before I moved in. Do you want to see the pose?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only if the pose has a name.\"",
                        Lands = "\"It's called The Winner. Obviously.\"",
                        Even = "\"It does. It's private.\"",
                        Grates = "\"It has a name. You don't get to know it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Show me. Right now.\"",
                        Lands = "(one hand on the railing, chin up) \"That's the pose. That's the finale.\"",
                        Even = "\"Not yet. Finale only.\"",
                        Grates = "\"You don't rush the pose.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Planning the finale is a jinx.\"",
                        Lands = "\"Maybe. But I'm not superstitious. Mostly.\"",
                        Even = "\"I'm not superstitious.\"",
                        Grates = "\"Don't put that into the universe.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.shampoo", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(holding up a bottle of house shampoo) \"Brutally honest review: two stars. Smells like a hotel. Lathers like a disappointment.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do the conditioner next.\"",
                        Lands = "\"One star. Don't get me started. You're a great audience.\"",
                        Even = "\"Later. I need to build suspense.\"",
                        Grates = "\"I'm not a vending machine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Two stars is generous.\"",
                        Lands = "\"Finally, someone with taste.\"",
                        Even = "\"I'm being nice. For once.\"",
                        Grates = "\"Two stars is the review. Don't edit me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"It's free shampoo. Just use it.\"",
                        Lands = "\"Ugh. Okay. You're right. Free is free.\"",
                        Even = "\"I'll use it. I'll complain.\"",
                        Grates = "\"Nothing is just anything with me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.whisper", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(nodding at two houseguests muttering in the hallway) \"See that? Whispering. If you've got something to say about me, say it to my face. Watch, I'll go ask.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go ask. I'll watch.\"",
                        Lands = "\"That's what I like to hear. Hold my water.\"",
                        Even = "\"Maybe later. When there's a crowd.\"",
                        Grates = "\"I'll go when I'm ready.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"They might not be talking about you.\"",
                        Lands = "\"Everyone's always talking about me. But okay, maybe.\"",
                        Even = "\"They are. I can tell.\"",
                        Grates = "\"Don't tell me to let it go. They're talking about me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You whisper about people too.\"",
                        Lands = "\"Ha. Fair. Said to my face. I respect it.\"",
                        Even = "\"Sometimes. Out loud, though.\"",
                        Grates = "\"I say it out loud, actually. Big difference.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.mirrors", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(checking hair in the sliding door's reflection) \"The house has six mirrors and four of them lie. I've tested all of them. This one's honest.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What does the honest one say?\"",
                        Lands = "\"That I'm right. As always.\"",
                        Even = "\"That I need sleep.\"",
                        Grates = "\"Not telling you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You look fine in all of them.\"",
                        Lands = "\"Fine is the floor. But okay, thank you.\"",
                        Even = "\"Fine is a word.\"",
                        Grates = "\"Fine. Right. Okay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Show me the honest one sometime.\"",
                        Lands = "\"Only for you. Second door past the bathroom.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It's mine. Find your own.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.argument", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(lying on the living-room floor, arms out) \"Nothing has happened for three hours. I'm going to start an argument just to feel something.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Argue with me. I'll take the wrong side.\"",
                        Lands = "\"Yes. Pineapple on pizza. Go.\"",
                        Even = "\"Okay. Topic?\"",
                        Grates = "\"You'd lose. Pass.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Quiet's nice for once.\"",
                        Lands = "\"Quiet is a vacuum. But okay, five more minutes.\"",
                        Even = "\"Quiet is fine. For a minute.\"",
                        Grates = "\"Quiet is the worst. Don't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Don't start anything. You'll regret it.\"",
                        Lands = "\"Regret is content. But fine. For you.\"",
                        Even = "\"No promises.\"",
                        Grates = "\"Don't tell me what I'll regret.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "quinn.rh.brunch", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(squinting at the sun through the yard netting) \"In LA I'd be at brunch right now, reviewing someone's lipstick. Here I'm reviewing the pool filter.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"How's the pool filter holding up?\"",
                        Lands = "\"Three stars. Honest. Reliable. Boring.\"",
                        Even = "\"It's fine. Unfortunately.\"",
                        Grates = "\"Don't steal my bit.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you miss it?\"",
                        Lands = "\"Brunch, yes. Everything else, less than I thought.\"",
                        Even = "\"Sometimes.\"",
                        Grates = "\"Obviously. Next question.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We could do brunch here. Eggs and gossip.\"",
                        Lands = "\"Eggs and gossip. Yes. You're hosting.\"",
                        Even = "\"Maybe Sunday.\"",
                        Grates = "\"Brunch needs a view. This is breakfast.\"",
                    },
                },
            },
        };
    }
}
