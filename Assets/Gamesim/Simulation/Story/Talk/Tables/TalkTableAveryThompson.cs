namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, Avery Thompson's own talks (slice S5, talk rules 5; spec 5.4; ruling R1: the talks are C# tables): a first
    /// talk and layer frames that lead to the next fact on their sheet, where they stand this week, the week's events, and house
    /// life, in their own voice. Every row carries Owner = <see cref="Owner"/> and <see cref="TalkRules.ComesToTalk"/>
    /// (<see cref="TalkRow.FromRules"/>), so no season below talk rules 5 is offered one. A fictional regular (Roster.Regular):
    /// no line names a real person; only the standing lines name a housemate, through their tokens; the event lines carry no
    /// token and no gendered word. Three replies a talk, each with its line for every landing. American spelling.
    /// </summary>
    public static class TalkTableAveryThompson
    {
        public const string Owner = "avery-thompson";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "avery.l1.first", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { First = true },
                Open = "(standing by the back door, watching who goes where) \"I like to know where the exits are. Habit. Ask me what you want to ask. I'll answer straight.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Then straight: who are you?\"",
                        Lands = "\"Good. Straight back. Here.\"",
                        Even = "\"Fair question. Short answer.\"",
                        Grates = "\"Bit much for a first talk. Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Glad you're here. Want to sit for a minute?\"",
                        Lands = "\"A minute. Sure. Here's something.\"",
                        Even = "\"I'll stand. Here's something.\"",
                        Grates = "\"I'm good standing. Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Exits? Planning a getaway already?\"",
                        Lands = "\"Not yet. Ask me again after the first vote. Here's something.\"",
                        Even = "\"Just a habit. Here.\"",
                        Grates = "\"It's not a joke. Tomorrow.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.l1.laps", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(running laps of the yard at dawn, slowing as you come out) \"You're up early. Walk with me. I don't stop till twenty, but I can talk.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll run with you. Try to keep up.\"",
                        Lands = "\"Ha. Okay. Talk while you can still breathe. Here.\"",
                        Even = "\"Sure. Talk while you can. Here.\"",
                        Grates = "\"Not today. Pace is set. Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll walk. You run. We'll talk on the turns.\"",
                        Lands = "\"That works. Turn one. Here.\"",
                        Even = "\"Fair. Here.\"",
                        Grates = "\"Too slow. Catch me after.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Twenty laps? You're a machine.\"",
                        Lands = "\"Something like that. Here's a thing about the machine.\"",
                        Even = "\"Just disciplined. Here.\"",
                        Grates = "\"It's just laps. Later.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.l1.doorway", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(at the edge of the kitchen, taking in the room before walking in) \"Three people in here are pretending not to argue. I read a room first. Then I come in. Ask me something.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Who's arguing?\"",
                        Lands = "\"Not my business to say. Something about me instead. Here.\"",
                        Even = "\"Not saying. Here's something else.\"",
                        Grates = "\"Not my business. Or yours.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What else do you read before you walk in?\"",
                        Lands = "\"Who's tired. Who's lying. Here's something about me.\"",
                        Even = "\"Plenty. Here's one thing.\"",
                        Grates = "\"That's my side of the room. Not yours.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do you ever just walk into a room?\"",
                        Lands = "\"Never. Here's why, sort of.\"",
                        Even = "\"No. Here.\"",
                        Grates = "\"No. And you just showed me why.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.l1.weights", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(racking the yard weights back in exact order) \"People leave these everywhere. Somebody's going to trip. Hand me the fives.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(hand over the fives)",
                        Lands = "\"Thanks. Quiet help is the best kind. Here's something.\"",
                        Even = "\"Thanks. Here.\"",
                        Grates = "\"Those are the tens. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll take the heavy ones. You do the small stuff.\"",
                        Lands = "\"Ha. Okay. Fair split. Here's something while we work.\"",
                        Even = "\"Sure. Here.\"",
                        Grates = "\"I've got the heavy ones. Fives.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're always looking out for everybody.\"",
                        Lands = "\"Somebody has to. Here's something about why.\"",
                        Even = "\"Habit. Here.\"",
                        Grates = "\"Just weights. Don't make it more.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.l2.word", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(on the hammock, one foot on the ground to steady it) \"In here, your word is all you've got. I keep mine. Costs me, sometimes. Ask me something real.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What does keeping it cost you?\"",
                        Lands = "\"Good question. Here's part of an answer.\"",
                        Even = "\"Plenty. Here's one thing.\"",
                        Grates = "\"More than I'll say right now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Everyone says they keep their word.\"",
                        Lands = "\"They do. Most don't. Here's something true.\"",
                        Even = "\"They say it. Here.\"",
                        Grates = "\"Not everyone means it. You'd know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Whatever you want to tell me.\"",
                        Lands = "\"Then here's something I don't say much.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Ask a real question.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.l2.sixoclock", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(watching the sun go down over the yard wall) \"Back in Dallas, this is when the shift changes. I still feel it at six. Sit. Ask.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Tell me something about home.\"",
                        Lands = "\"Okay. One thing. Here.\"",
                        Even = "\"A little. Here.\"",
                        Grates = "\"Not home. Not today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"What's the part you don't tell the house?\"",
                        Lands = "\"Fair push. Here.\"",
                        Even = "\"Some of it. Here.\"",
                        Grates = "\"Push less. Get more.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What do you miss most out there?\"",
                        Lands = "\"Simple stuff. And one thing that isn't simple. Here.\"",
                        Even = "\"Plenty. Here's one.\"",
                        Grates = "\"Not here. Ask something else.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.l2.pushups", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(doing slow push-ups by the pool, counting under the breath) \"Forty. I think best when I'm tired. Ask me something you've been sitting on.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Okay. What makes you dangerous?\"",
                        Lands = "\"Ha. Fair. Here's part of it.\"",
                        Even = "\"Here's one thing.\"",
                        Grates = "\"That's for me to know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You count out loud like a drill sergeant.\"",
                        Lands = "\"Forty-one. Fine. Here's something, recruit.\"",
                        Even = "\"Forty-one. Here.\"",
                        Grates = "\"You made me lose count.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Finish your set first.\"",
                        Lands = "\"Appreciated. Fifty. Okay. Here.\"",
                        Even = "\"Fifty. Here.\"",
                        Grates = "\"I can talk and count.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.l3.line", Owner = Owner, Kind = TalkKind.Layer, Layer = 3, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 3 },
                Open = "(in the yard after dark, voice low, eyes on the house) \"There's one thing I don't forgive. Even when it isn't done to me. You should know what it is.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Tell me. I'll stay on the right side of it.\"",
                        Lands = "\"Good. Here. Stay there.\"",
                        Even = "\"Here.\"",
                        Grates = "\"Saying it is easy. Another time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only if you want me to know.\"",
                        Lands = "\"I want you to. Here.\"",
                        Even = "\"I do. Here.\"",
                        Grates = "\"I said you should. Listen.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Is that a warning?\"",
                        Lands = "\"It's information. Here.\"",
                        Even = "\"It's a fact. Here.\"",
                        Grates = "\"If you heard a warning, maybe you needed one.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.st.bench", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(on the bench at the far end of the yard, out of earshot) \"You want to know where I stand. I'll tell you once. No second version.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Once is all I need.\"",
                        Lands = "\"Good. Here it is.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"Then listen close. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Names. Don't dress it up.\"",
                        Lands = "\"Never do. Here.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"You'll get what I give you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Only if you trust me with it.\"",
                        Lands = "\"I do. Here.\"",
                        Even = "\"Mostly. Here.\"",
                        Grates = "\"Trust is earned. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"I'd stand in front of {close} any day. {worry} I keep an eye on. Me? {safe}. That's it. No second version.\"",
                    Neutral = "\"There's someone I'd stand in front of. Someone I keep an eye on. Me? {safe}. That's it.\"",
                    Dishonest = "\"I'd stand in front of {close} any day. {worry} I keep an eye on. Me? I'm solid. That's it.\"",
                },
            },
            new TalkRow
            {
                Id = "avery.st.dawn", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(stretching after the morning laps, still breathing hard) \"Nobody's up. Good time for a straight answer. Ask.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Catch your breath first.\"",
                        Lands = "\"Appreciated. Okay. Here.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"I'm fine. Ask.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Where are you this week?\"",
                        Lands = "\"Straight. Here.\"",
                        Even = "\"Here.\"",
                        Grates = "\"Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's in your circle? Exactly.\"",
                        Lands = "\"Exactly. Okay.\"",
                        Even = "\"Close enough.\"",
                        Grates = "\"Exactly is a lot to ask. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"{close} has my back and I've got theirs. {worry} is trouble. Me? {safe}. Now let's run.\"",
                    Neutral = "\"Somebody has my back and I've got theirs. Somebody's trouble. Me? {safe}. Now let's run.\"",
                    Dishonest = "\"{close} has my back and I've got theirs. {worry} is trouble. Me? Steady. Now let's run.\"",
                },
            },
            new TalkRow
            {
                Id = "avery.event.player-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerHoH, Window = TalkWindows.AfterHoH },
                Open = "(knocking once on the HoH door frame, not coming in) \"Congratulations. I won't come in and pitch. You know where I stand. That's all I came to say.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. Thank you for that.\"",
                        Lands = "\"Good. Enjoy the room.\"",
                        Even = "\"Okay. Enjoy it.\"",
                        Grates = "\"Don't thank me. Just remember it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Come in anyway. Have a snack.\"",
                        Lands = "\"One. Then I'm out of your way.\"",
                        Even = "\"I'm good. Thanks.\"",
                        Grates = "\"No. Doesn't look right.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Everybody says they're not pitching.\"",
                        Lands = "\"I'm not everybody. Night.\"",
                        Even = "\"I'm not pitching.\"",
                        Grates = "\"Then don't listen to everybody.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.event.npc-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcHoH, Window = TalkWindows.AfterHoH },
                Open = "(on the HoH bed, elbows on knees) \"I won it. Now I've got to put two people up. I don't like it. I'll do it right, though.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You'll be fair. That's who you are.\"",
                        Lands = "\"That's the idea. Fair, and to their faces.\"",
                        Even = "\"Trying.\"",
                        Grates = "\"Fair isn't the same as easy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go big. Take a real shot.\"",
                        Lands = "\"A real shot. Yeah. I hear you.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"I'll take the shot I can stand behind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Big room for one person.\"",
                        Lands = "\"Echoes. I'll sleep on the floor, probably.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"Not the time.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.event.player-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerNominated, Window = TalkWindows.AfterNoms },
                Open = "(sitting down across from you, steady) \"You're on the block. I want you to know I'm not going anywhere. What do you need?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I need your vote. Plainly.\"",
                        Lands = "\"You've got it. Plainly.\"",
                        Even = "\"Let me see the board. You'll probably have it.\"",
                        Grates = "\"Ask me after the veto.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Just that. Thank you.\"",
                        Lands = "\"Then you've got it.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"That's not a plan. Think about it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"If you're not voting for me, tell me now.\"",
                        Lands = "\"Fair. I'll tell you now. I'm with you.\"",
                        Even = "\"I'll tell you when I know.\"",
                        Grates = "\"Don't push me like that. I said I'm here.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.event.npc-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcNominated, Window = TalkWindows.AfterNoms },
                Open = "(doing push-ups by the nomination chairs, not counting out loud) \"I'm up. Okay. I don't beg. I compete. Veto's next.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then win it. Simple.\"",
                        Lands = "\"That's the plan. Simple.\"",
                        Even = "\"Planning on it.\"",
                        Grates = "\"I know what to do.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Whatever happens, I've got your back.\"",
                        Lands = "\"That means a lot. I'll remember it.\"",
                        Even = "\"Thanks.\"",
                        Grates = "\"Don't. I don't need anybody's back.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who put you there, really?\"",
                        Lands = "\"Not just the HoH. I'll figure out the rest.\"",
                        Even = "\"Working on it.\"",
                        Grates = "\"Not now. Veto first.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.event.npc-veto", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcVeto, Window = TalkWindows.AfterVeto },
                Open = "(hanging the veto on the bedpost) \"Won it. Made my call at the meeting, and I'd make it again. That's the part I came here for.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You stood by your call. People noticed.\"",
                        Lands = "\"Good. That's all I want them to notice.\"",
                        Even = "\"Let them.\"",
                        Grates = "\"I don't do it to be noticed.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"It was your call. I respect it.\"",
                        Lands = "\"That means something. Thanks.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Respect's earned over weeks. Not one meeting.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Some people won't forget that call.\"",
                        Lands = "\"Good. Then they'll know where I stand.\"",
                        Even = "\"That's on them.\"",
                        Grates = "\"Is that a threat? Don't.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.event.ally-evicted", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.AllyEvicted, Window = TalkWindows.AfterEviction },
                Open = "(standing at the door the evictee walked out of) \"They had my back from day one. I couldn't stop it. That sits heavy.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(stand there a while, saying nothing)",
                        Lands = "\"Thanks. That's what I needed.\"",
                        Even = "\"Yeah.\"",
                        Grates = "\"You don't have to stand here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You did what you could.\"",
                        Lands = "\"I did. Doesn't make it lighter.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Not enough, I didn't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then make it count next week.\"",
                        Lands = "\"I will. Count on it.\"",
                        Even = "\"Planning on it.\"",
                        Grates = "\"Give it a day.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.event.couple-noticed", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.CoupleNoticed },
                Open = "(stopping you in the hall) \"House knows about you two. Heads up: that makes a target. Anybody comes for you, they come through me first.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. That means a lot.\"",
                        Lands = "\"Just keep your word to me. That's all.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Don't thank me. Watch your back.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"We'll be careful.\"",
                        Lands = "\"Good. Careful wins weeks.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Careful wasn't enough last week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Through you? That's a big wall.\"",
                        Lands = "\"Biggest in the house. Use it.\"",
                        Even = "\"It's a wall.\"",
                        Grates = "\"It's not a joke.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.onesecond", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(icing a shoulder after the HoH comp) \"Gave that comp everything I had. Shoulder's paying for it. Worth it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Next one, give it more.\"",
                        Lands = "\"Ha. There isn't more. But I'll find some.\"",
                        Even = "\"Always do.\"",
                        Grates = "\"I gave all of it. Didn't you see?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You're icing like you ran a marathon.\"",
                        Lands = "\"Felt like one. Hand me the other bag.\"",
                        Even = "\"It was a long comp.\"",
                        Grates = "\"Laugh it up. Your turn next week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Rest it tonight. No laps tomorrow.\"",
                        Lands = "\"Yeah. You're right. One day off.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Laps are how I rest.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.hoops", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(shooting baskets alone in the yard) \"Nominations always make the house loud. I come out here till it settles. Want to shoot?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Loser does the dishes.\"",
                        Lands = "\"You're on. Check ball.\"",
                        Even = "\"Sure. Your ball.\"",
                        Grates = "\"I don't bet. Just shoot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll rebound. You shoot.\"",
                        Lands = "\"Deal. Good way to settle.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Shoot. It's better.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Sure. It's good to get out of there.\"",
                        Lands = "\"It is. Your ball.\"",
                        Even = "\"Your ball.\"",
                        Grates = "\"Just shoot.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.medal", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(watching the veto medal get passed around the kitchen) \"Everybody wants to try it on. I'd wear one under my shirt. Nobody needs to see that.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You'd hide a win?\"",
                        Lands = "\"Not hide. Wear it quiet.\"",
                        Even = "\"Some of them.\"",
                        Grates = "\"Not hide. Different thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You'd sleep in it, though.\"",
                        Lands = "\"Every night. Don't tell anybody.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"No. That's weird.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Next one's yours.\"",
                        Lands = "\"Yeah. It is.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"I don't need a cheerleader.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.hinge", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(fixing a squeaky hinge on the front door with a butter knife) \"Every week that door opens and someone walks out. Least I can do is make it quiet.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(hold the door steady)",
                        Lands = "\"Thanks. Little to the left. Good.\"",
                        Even = "\"Good. Hold it.\"",
                        Grates = "\"I've got it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Fixing the door won't change who goes.\"",
                        Lands = "\"No. But it's something I can fix.\"",
                        Even = "\"No.\"",
                        Grates = "\"I know that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"With a butter knife?\"",
                        Lands = "\"You work with what's in the drawer.\"",
                        Even = "\"It works.\"",
                        Grates = "\"It's what we have.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.u.checkin", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(one nod from the bench, water bottle in hand) \"You good? Anybody giving you trouble?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm good. No trouble.\"",
                        Lands = "\"Good. Keep it that way.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Tell me if that changes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Just you, staring at me.\"",
                        Lands = "\"Ha. Fair. I'll look somewhere else.\"",
                        Even = "\"Just checking.\"",
                        Grates = "\"I'm watching the yard, not you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(nod back)",
                        Lands = "\"Good. That's enough.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Words work too.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.u.laps", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(lacing up shoes on the back step) \"Laps. Same as always. You coming or watching?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Coming. Don't wait for me.\"",
                        Lands = "\"Never do. Let's go.\"",
                        Even = "\"Let's go.\"",
                        Grates = "\"Keep up, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Watching. Somebody has to count.\"",
                        Lands = "\"Twenty. Count out loud.\"",
                        Even = "\"Count, then.\"",
                        Grates = "\"Count right this time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Have a good run.\"",
                        Lands = "\"Will do.\"",
                        Even = "\"Yep.\"",
                        Grates = "\"It's laps. Not a trip.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.clearer", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 3 },
                Open = "(looking at the memory wall) \"A few weeks in. People I'd have sworn by on day one, I don't trust now. People I didn't, I do.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Where do I land on that list?\"",
                        Lands = "\"Right side. So far.\"",
                        Even = "\"Still deciding.\"",
                        Grates = "\"Don't ask for a grade.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"That's how it goes in here.\"",
                        Lands = "\"It is. Takes time to read people.\"",
                        Even = "\"Yeah.\"",
                        Grates = "\"Doesn't make it easier.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who flipped on you?\"",
                        Lands = "\"Nobody flipped. I just saw them clearer.\"",
                        Even = "\"Not saying.\"",
                        Grates = "\"That's mine to keep.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.kept", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 6 },
                Open = "(sitting on the edge of the pool, feet in the water) \"The house keeps getting smaller. I made promises in here. I'm going to keep every one, even the ones that hurt.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I believe you.\"",
                        Lands = "\"Good. Hold me to it.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Watch me. Don't just believe me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Even if one costs you the game?\"",
                        Lands = "\"Even then. That's the point of a promise.\"",
                        Even = "\"Even then.\"",
                        Grates = "\"I said every one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Even the promise to stop eating my cereal?\"",
                        Lands = "\"That one I might break.\"",
                        Even = "\"That one's under review.\"",
                        Grates = "\"That's not a promise. That's a crime.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.grill", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(manning the backyard grill like a post) \"Nobody touches the grill but me. Not a rule. Just how it's going to be.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is that an order, officer?\"",
                        Lands = "\"Ha. It's a strong suggestion.\"",
                        Even = "\"It's a suggestion.\"",
                        Grates = "\"Don't call me that in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Let me flip one. One.\"",
                        Lands = "\"One. Watch the flip. Good.\"",
                        Even = "\"Maybe tomorrow.\"",
                        Grates = "\"No. Grill's mine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Whatever you're making smells great.\"",
                        Lands = "\"Thanks. First plate's yours.\"",
                        Even = "\"It's just burgers.\"",
                        Grates = "\"It's not done yet. Wait.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.third", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(studying the corner of the yard where last week's comp stood) \"I replay every comp in my head. Every slip. Anything short of first bugs me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then go get first next time.\"",
                        Lands = "\"Exactly. That's the plan.\"",
                        Even = "\"Working on it.\"",
                        Grates = "\"I know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Doing your best is good enough.\"",
                        Lands = "\"Maybe. Still bugs me. But maybe.\"",
                        Even = "\"Not for me.\"",
                        Grates = "\"Good enough is how you go home.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Anything short of first? That's dramatic.\"",
                        Lands = "\"Ha. Little bit. I'm competitive.\"",
                        Even = "\"It's true, though.\"",
                        Grates = "\"Not dramatic. Accurate.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.watch", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(awake on the couch at night, one eye on the hallway) \"I don't sleep much in here. Somebody should keep an eye on things. Might as well be me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Get some rest. The house will be fine.\"",
                        Lands = "\"You might be right. An hour, maybe.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Fine isn't a plan.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Nobody asked you to guard the house.\"",
                        Lands = "\"Nobody had to.\"",
                        Even = "\"I know.\"",
                        Grates = "\"Nobody asks. That's the point.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Night watch. Very serious.\"",
                        Lands = "\"Very serious. You're relieved of duty.\"",
                        Even = "\"It's habit.\"",
                        Grates = "\"It is serious.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.thermostat", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(frowning at the thermostat) \"Seventy-two in here. In Dallas this time of year I'd be out in a hundred. This is a vacation.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You're bragging about heat.\"",
                        Lands = "\"Ha. Little bit.\"",
                        Even = "\"Just saying.\"",
                        Grates = "\"It's not bragging. It's facts.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"So it's too cold for you?\"",
                        Lands = "\"Little cold. I'll survive.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"Didn't say that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Want a blanket?\"",
                        Lands = "\"Ha. I'm fine. Appreciate it, though.\"",
                        Even = "\"I'm good.\"",
                        Grates = "\"I'm not cold.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "avery.rh.handshake", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(holding out a hand after a game of cards) \"Good game. I shake hands, win or lose. You learn a lot from a handshake.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "(shake the hand firmly)",
                        Lands = "\"Firm. Good. I like that.\"",
                        Even = "\"Good game.\"",
                        Grates = "\"Too firm. Trying to prove something?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What did mine tell you?\"",
                        Lands = "\"That you'd rather win. Same as me.\"",
                        Even = "\"Enough.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Good game. You won fair.\"",
                        Lands = "\"I did. You lost fair. That counts.\"",
                        Even = "\"Thanks.\"",
                        Grates = "\"You let me win. Don't do that.\"",
                    },
                },
            },
        };
    }
}
