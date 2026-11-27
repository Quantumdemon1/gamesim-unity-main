namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, Riley Johnson's own talks (slice S5, talk rules 5; spec 5.4; ruling R1: the talks are C# tables): a first
    /// talk and layer frames that lead to the next fact on their sheet, where they stand this week, the week's events, and house
    /// life, in their own voice. Every row carries Owner = <see cref="Owner"/> and <see cref="TalkRules.ComesToTalk"/>
    /// (<see cref="TalkRow.FromRules"/>), so no season below talk rules 5 is offered one. A fictional regular (Roster.Regular):
    /// no line names a real person; only the standing lines name a housemate, through their tokens; the event lines carry no
    /// token and no gendered word. Three replies a talk, each with its line for every landing. American spelling.
    /// </summary>
    public static class TalkTableRileyJohnson
    {
        public const string Owner = "riley-johnson";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "riley.l1.first", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { First = true },
                Open = "(sitting on the bottom bunk, sorting socks into exact pairs) \"Hi. I'm told the move is to swap one fact each. I've prepared mine. It's short.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then let's trade. One fact each, same size.\"",
                        Lands = "\"Same size. Good. That's a fair protocol. Here's mine.\"",
                        Even = "\"Fine. I'll go first. Keep yours under a minute.\"",
                        Grates = "\"You said trade and then waited. That's not a trade. Ask me tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Short is fine. I'd like to hear it.\"",
                        Lands = "\"Okay. You're easier to talk to than I modeled. Here.\"",
                        Even = "\"Okay. Here it is. Don't expect a story.\"",
                        Grates = "\"You're being nice on purpose. I can tell. Ask me tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You prepared? Did you rehearse it in the mirror?\"",
                        Lands = "\"Twice. Out loud. You get the rehearsed version anyway.\"",
                        Even = "\"Once. It's still true. Here.\"",
                        Grates = "\"Wow. One talk in and I'm already the joke. Never mind.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.l1.whiteboard", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(drawing a grid on the kitchen whiteboard) \"I'm mapping who talks to who. It's not sinister. It's just a diagram.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's a little sinister. What's my square say?\"",
                        Lands = "\"Fair. Mostly blank. Here's a cell about me, so we're even.\"",
                        Even = "\"Blank, mostly. Here's one about me so we're even.\"",
                        Grates = "\"It says asks too many questions. Erasing it now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Show me the grid and I'll tell you what's missing.\"",
                        Lands = "\"That's the most useful offer I've had in here. One thing about me, for the trade.\"",
                        Even = "\"Maybe. Here's one thing about me first.\"",
                        Grates = "\"The grid stays mine. Nice try.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Erase it before somebody sees it.\"",
                        Lands = "\"You're right. Erasing. Here's something about me so the minute isn't wasted.\"",
                        Even = "\"Fine. Erased. You still get one thing.\"",
                        Grates = "\"I don't take orders about my own diagrams. Goodbye.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.l1.lightsout", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(in the dark bedroom, only a sliver of hall light) \"Leave the lights off, please. I think better when I can't see anyone's face.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit down in the dark and say nothing)",
                        Lands = "\"Thank you. Silence is underrated. Since you're here, one thing about me.\"",
                        Even = "\"Okay. You can stay. Here's one thing.\"",
                        Grates = "\"You can't just sit there. That's weirder than talking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Lights stay off. I'll keep you company.\"",
                        Lands = "\"Okay. That's nice. Unexpectedly nice. Here.\"",
                        Even = "\"Okay. Company accepted. One fact, then quiet.\"",
                        Grates = "\"I said I think better alone. Company isn't alone.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this your evil-genius thinking spot?\"",
                        Lands = "\"Ha. Yes. Every genius needs one. Here's a fact from it.\"",
                        Even = "\"It's a bedroom. But fine. One fact.\"",
                        Grates = "\"Could you not say that where people can hear? Goodnight.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.l1.puzzle", Owner = Owner, Kind = TalkKind.Layer, Layer = 1, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(bent over a jigsaw on the dining table, three edges done) \"Nobody helps with the puzzle. You can, if you do edges and don't talk strategy.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "(sort the edge pieces into a neat row)",
                        Lands = "\"You sort. I respect a person who sorts. One thing about me, as a thank you.\"",
                        Even = "\"Good method. Fine. One fact while we work.\"",
                        Grates = "\"Wrong row. Those are sky. Never mind, I'll do it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm bad at puzzles. I'll just keep you company.\"",
                        Lands = "\"Honest. I like honest. Sit. Here's something about me.\"",
                        Even = "\"That's allowed. Sit. One fact, no strategy.\"",
                        Grates = "\"Then why sit at the puzzle? Odd choice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Forget the puzzle. Talk to me about the vote.\"",
                        Lands = "\"Puzzle first. Vote never. But you get one thing about me for trying.\"",
                        Even = "\"Not the vote. Here's something else.\"",
                        Grates = "\"I said no strategy. That was the one rule.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.l2.log", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(writing in a notebook, then closing it when you sit) \"I keep a log in here. Mostly competitions. Some of it's about me. I don't share that part.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"You don't have to share it all. Just the part that matters.\"",
                        Lands = "\"You framed that well. Fine. One entry from the part I don't share.\"",
                        Even = "\"Fair. One entry.\"",
                        Grates = "\"Every part matters. That's why it's in the log. No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd like to read the part about you, if you'd let me.\"",
                        Lands = "\"Okay. Not read. But I'll tell you one entry.\"",
                        Even = "\"Not read. Hear. One entry.\"",
                        Grates = "\"That's a lot of warmth at once. Overloading. No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You can't keep a log on all of us and share nothing back.\"",
                        Lands = "\"That's a fair point. Annoying, but fair. Here.\"",
                        Even = "\"Fine. One thing back. That's the exchange rate.\"",
                        Grates = "\"I can, actually. It's my notebook.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.l2.code", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(staring at the ceiling, fingers tapping like a keyboard) \"I keep writing code in my head. No computer in here. My hands don't know what to do.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"So what's the program?\"",
                        Lands = "\"It's me, mostly. Debugging. Here's one line of it.\"",
                        Even = "\"A small one. Here's a piece of it.\"",
                        Grates = "\"You wouldn't follow it. That's not an insult. Mostly.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Type me a message on the ceiling.\"",
                        Lands = "\"Ha. Okay. Message: here's something true.\"",
                        Even = "\"Message received. Fine. One thing.\"",
                        Grates = "\"That's the joke everyone makes. You're everyone now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Your hands can rest. Nobody's grading you in here.\"",
                        Lands = "\"Everybody's grading everybody in here. But thanks. Here's something real.\"",
                        Even = "\"Everyone's grading. But okay. One thing.\"",
                        Grates = "\"Easy to say. Never mind.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.l2.cue", Owner = Owner, Kind = TalkKind.Layer, Layer = 2, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 2 },
                Open = "(after a group laugh, still a beat behind it) \"Everyone laughed and I laughed three seconds late. I run a little behind on cues. You noticed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I didn't notice. And if I had, I wouldn't care.\"",
                        Lands = "\"You did notice. But that was kind. Here's something, since you were kind.\"",
                        Even = "\"Okay. I'll take that. One thing about me.\"",
                        Grates = "\"You noticed. Don't pretend. Forget it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Three seconds is fine. Nobody's timing it but you.\"",
                        Lands = "\"Ha. True. I am timing it. Okay. One real thing.\"",
                        Even = "\"I am timing it. Fine. Here's one thing.\"",
                        Grates = "\"Somebody always times it. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Three seconds? I clocked four.\"",
                        Lands = "\"Four. Fine. You're funny. Here's a fact for the funny one.\"",
                        Even = "\"Three point five. Whatever. One fact.\"",
                        Grates = "\"Great. Now there are two people timing it. I'm done.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.l3.never", Owner = Owner, Kind = TalkKind.Layer, Layer = 3, Reveals = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 3 },
                Open = "(in the dark bedroom, voice low) \"I came in wanting one reliable person. Just one. Can I tell you the thing I'd never get past?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only if you want to. I'll still be here if you don't.\"",
                        Lands = "\"That's why I want to. Okay. Listen.\"",
                        Even = "\"I want to. Mostly. Here.\"",
                        Grates = "\"Don't make it optional. I already decided. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Tell me. I'll remember it.\"",
                        Lands = "\"Good. Remember it exactly. Here.\"",
                        Even = "\"Okay. Here it is.\"",
                        Grates = "\"Too quick. That was too quick. Another time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Tell me, and I'll make sure I never do it.\"",
                        Lands = "\"That's the right answer. That's exactly the right answer. Here.\"",
                        Even = "\"Okay. Here's the rule.\"",
                        Grates = "\"You sound like you're taking notes for later. Not tonight.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.st.numbers", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(in the storage room, counting cans to look busy) \"You want my read on the week. I ran it twice. Same answer both times.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Give me the numbers. I'll keep them to myself.\"",
                        Lands = "\"Good. Then here's the whole model.\"",
                        Even = "\"Okay. The short model.\"",
                        Grates = "\"Not numbers for you. Words. Fewer of them.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Just tell me where you are.\"",
                        Lands = "\"Okay. Directly. Here.\"",
                        Even = "\"Fine. Short version.\"",
                        Grates = "\"You get the summary. Not the data.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Names. Now, before someone walks in.\"",
                        Lands = "\"Right. Fast. Here.\"",
                        Even = "\"Fine. Quick.\"",
                        Grates = "\"Pushing me makes me slower. Summary only.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"{close} is the one I'd trust with a vote. {worry} is the variable I can't solve. Me? {safe}. That's the model.\"",
                    Neutral = "\"There's one person I'd trust with a vote. One variable I can't solve. Me? {safe}. That's the summary.\"",
                    Dishonest = "\"{close} is the one I'd trust with a vote. {worry} is the variable I can't solve. Me? Stable. That's the model.\"",
                },
            },
            new TalkRow
            {
                Id = "riley.st.dark", Owner = Owner, Kind = TalkKind.Standing, Layer = 4, Priority = 70, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 4 },
                Open = "(lights off, sitting up on the bunk) \"Lights are off. Nobody can read my face. Ask me where I stand. I'll answer once.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only what you want to say.\"",
                        Lands = "\"Then I'll say all of it. Here.\"",
                        Even = "\"Okay. Most of it.\"",
                        Grates = "\"Ask properly or don't. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Once is enough. I'll write nothing down.\"",
                        Lands = "\"Good. Then here's everything.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"You'd write it down. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"In the dark? Very spy movie.\"",
                        Lands = "\"It is a little spy movie. Fine. Here.\"",
                        Even = "\"Whatever. Here.\"",
                        Grates = "\"Don't. Short version, and then sleep.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"I'd go to the end with {close}. I don't trust {worry} at all. Me? {safe}. Now go to sleep.\"",
                    Neutral = "\"There's someone I'd go to the end with. Someone I don't trust. Me? {safe}. Now go to sleep.\"",
                    Dishonest = "\"I'd go to the end with {close}. I don't trust {worry} at all. Me? I'm fine. Now go to sleep.\"",
                },
            },
            new TalkRow
            {
                Id = "riley.event.player-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerHoH, Window = TalkWindows.AfterHoH },
                Open = "(knocking on the HoH door exactly twice) \"I'm not here to campaign. I want to see what the room looks like from the inside. For the data.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Come in. Look around. Ask me nothing about nominations.\"",
                        Lands = "\"Deal. I wasn't going to. I was going to look at the ceiling. It's higher.\"",
                        Even = "\"Okay. No questions. Just the ceiling.\"",
                        Grates = "\"I said not campaigning. You didn't believe me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Come in. Want some of the snacks?\"",
                        Lands = "\"Yes. Thank you. I didn't expect to be fed.\"",
                        Even = "\"One. For the data.\"",
                        Grates = "\"That feels like a test. I'll pass.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You knocked twice. Was that a code?\"",
                        Lands = "\"It was a habit. But now it's a code. Keep it.\"",
                        Even = "\"It's how many knocks you need. Science.\"",
                        Grates = "\"Everyone teases the knocking. I'll go.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.event.npc-hoh", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcHoH, Window = TalkWindows.AfterHoH },
                Open = "(on the HoH bed with a notepad, not touching the snacks) \"I won. I have a key, a decision and about forty possible outcomes. Thirty-one are bad.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Walk me through the nine good ones.\"",
                        Lands = "\"Okay. You'd actually listen. Sit. Bring a pen.\"",
                        Even = "\"Some of them. Not all nine.\"",
                        Grates = "\"That's my work. Not yours.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You'll make a good call. You always think first.\"",
                        Lands = "\"I do think first. Thank you for noticing that.\"",
                        Even = "\"Thinking first is the easy part.\"",
                        Grates = "\"Thinking isn't deciding. Different problem.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Just pick and stop counting.\"",
                        Lands = "\"Ha. Maybe. That's one model.\"",
                        Even = "\"I'll pick. After I count.\"",
                        Grates = "\"Picking without counting is how people go home. No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.event.player-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.PlayerNominated, Window = TalkWindows.AfterNoms },
                Open = "(sitting down next to you after the ceremony, at a careful distance) \"You're on the block. I ran the numbers. They're not great, but they're not zero.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What would change them?\"",
                        Lands = "\"One vote. Two to be safe. I'll tell you which ones.\"",
                        Even = "\"A vote or two. I'm still working on it.\"",
                        Grates = "\"Too early to say. Ask me after the veto.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thanks for sitting with me.\"",
                        Lands = "\"It seemed like the right move. I'm not good at this part. I'm trying.\"",
                        Even = "\"You're welcome. I'll stay a minute.\"",
                        Grates = "\"I came with numbers, not comfort. Sorry.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I don't want numbers right now.\"",
                        Lands = "\"Okay. No numbers. I'll just sit.\"",
                        Even = "\"Okay. I'll keep them for later.\"",
                        Grates = "\"Numbers are all I've got. Fine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.event.npc-nominated", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcNominated, Window = TalkWindows.AfterNoms },
                Open = "(lying on the bunk with the lights off, staring up) \"I'm on the block. I'd like it noted that I predicted this. That doesn't help at all.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then let's count. Who's still open?\"",
                        Lands = "\"Yes. Thank you. Counting helps. Two people. Maybe three.\"",
                        Even = "\"Two. Maybe. I'll check again.\"",
                        Grates = "\"I already counted. Six times.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're not going anywhere. I'll make sure of it.\"",
                        Lands = "\"You can't make sure. But I'll take it.\"",
                        Even = "\"Okay. I'll take that as data.\"",
                        Grates = "\"You can't promise that. Please don't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You predicted it? Then you should've hidden better.\"",
                        Lands = "\"Ha. I tried. I'm bad at hiding. Obviously.\"",
                        Even = "\"I was hiding. It didn't work.\"",
                        Grates = "\"Thanks. That's exactly what I needed. Nothing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.event.npc-veto", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.NpcVeto, Window = TalkWindows.AfterVeto },
                Open = "(holding the veto medal like it might break) \"I won the veto. One win, right moment. That was the plan all along. It actually worked.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It worked. You should be proud.\"",
                        Lands = "\"I am. Quietly. Very quietly.\"",
                        Even = "\"I am. A little.\"",
                        Grates = "\"Proud is for after the vote.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who thanked you after the meeting, and who didn't?\"",
                        Lands = "\"I logged both lists. The second one is shorter. And more useful.\"",
                        Even = "\"Two did. I noted the rest.\"",
                        Grates = "\"That's my information to hold.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Now win the next one too.\"",
                        Lands = "\"That's the plan. Two in a row would surprise everyone. Including me.\"",
                        Even = "\"I'll try. The odds are worse.\"",
                        Grates = "\"One at a time. That's how this works.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.event.ally-evicted", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.AllyEvicted, Window = TalkWindows.AfterEviction },
                Open = "(making the empty bed with very square corners) \"They were my one reliable person. I planned the whole game around one. Now I have to plan again.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm sorry. That's a real loss.\"",
                        Lands = "\"It is. Thank you for saying it plainly.\"",
                        Even = "\"It is. I'll recalculate.\"",
                        Grates = "\"I don't need sympathy. I need a new plan.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then plan with me. I'm still here.\"",
                        Lands = "\"That's a real offer. I'm writing it down.\"",
                        Even = "\"Maybe. I'll think about it.\"",
                        Grates = "\"Too soon. The bed's not even cold.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take tonight. Plan tomorrow.\"",
                        Lands = "\"Okay. Tomorrow. That's sensible.\"",
                        Even = "\"Fine. Tomorrow.\"",
                        Grates = "\"Tomorrow's too late. The house moves fast.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.event.couple-noticed", Owner = Owner, Kind = TalkKind.Event, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Event = TalkEvents.CoupleNoticed },
                Open = "(looking up from the puzzle as you pass) \"The whole house knows about you two now. I'm told I was the last to notice. Statistically, I usually am.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"We're not hiding it anymore.\"",
                        Lands = "\"Good. Hiding things costs more than people think.\"",
                        Even = "\"Okay. Noted.\"",
                        Grates = "\"Not hiding it is a choice. A loud one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Does it make us a target?\"",
                        Lands = "\"Yes. Pairs always are. But a known pair is easier to defend.\"",
                        Even = "\"Probably. Pairs usually are.\"",
                        Grates = "\"You're asking me to run your numbers now?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Last to notice? Really?\"",
                        Lands = "\"Really. I thought you two just liked the same cereal.\"",
                        Even = "\"Really. I don't track that kind of thing.\"",
                        Grates = "\"Yes. Everyone enjoyed that. Moving on.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.steps", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(counting the steps up to the HoH room under the breath) \"Fourteen steps. I count them every week. New key, same fourteen steps.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you ever count anything just for fun?\"",
                        Lands = "\"This is for fun. Ask me about the floor tiles sometime.\"",
                        Even = "\"This is fun. For me.\"",
                        Grates = "\"I didn't say it was for fun.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Fourteen? I've only ever counted thirteen.\"",
                        Lands = "\"You skipped the landing. Classic. It's fourteen.\"",
                        Even = "\"Fourteen. I checked twice.\"",
                        Grates = "\"It's fourteen. I'm not doing this with you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's oddly comforting that you count them.\"",
                        Lands = "\"It is to me too. Same number every week. Nothing else in here is.\"",
                        Even = "\"It's a habit.\"",
                        Grates = "\"It's not comforting. It's accurate.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.chairs", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(straightening the nomination chairs until they sit exactly parallel) \"They were crooked all ceremony. Nobody else noticed. That's my contribution to the week.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(help line them up)",
                        Lands = "\"Thank you. Two people with the same standards. Rare in here.\"",
                        Even = "\"Okay. Left one, a little more.\"",
                        Grates = "\"That one's worse now. Leave it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"There's a lot going on this week besides chairs.\"",
                        Lands = "\"I know. Chairs are the part I can fix.\"",
                        Even = "\"I know. Chairs first.\"",
                        Grates = "\"I know that. Obviously I know that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Leave the chairs. Talk to me about the vote.\"",
                        Lands = "\"After the chairs. Then maybe.\"",
                        Even = "\"The chairs aren't done.\"",
                        Grates = "\"No. The chairs are what I'm doing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.odds", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(with a pencil behind one ear) \"Veto's done. I'd put the odds of a quiet week at about twelve percent. Generous.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Twelve? What's in the other eighty-eight?\"",
                        Lands = "\"Arguments, mostly. Two blindsides. One burnt dinner.\"",
                        Even = "\"Mostly yelling. Some cooking.\"",
                        Grates = "\"Too many variables to list.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You made that number up.\"",
                        Lands = "\"I did. But I made it up carefully.\"",
                        Even = "\"Roughly. Roughly up.\"",
                        Grates = "\"I did not. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Let's make it a quiet one, then.\"",
                        Lands = "\"Okay. Thirteen percent. You moved it a point.\"",
                        Even = "\"I'll update the number. Slightly.\"",
                        Grates = "\"That's not how odds work.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.heads", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(looking around the living room, lips moving) \"Counting heads. We're one fewer. I know that. I count anyway.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Count. I'll wait.\"",
                        Lands = "\"Done. Thank you for waiting. People never wait.\"",
                        Even = "\"Done. Same number as last time I counted.\"",
                        Grates = "\"Don't wait. It makes it weird.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's a lot, every week. You okay?\"",
                        Lands = "\"I think so. Counting helps. Asking helps too.\"",
                        Even = "\"Okay enough. Thanks.\"",
                        Grates = "\"I'm counting, not crying. Different activity.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It never gets easier, does it.\"",
                        Lands = "\"No. It gets more predictable. That's not the same.\"",
                        Even = "\"No. Not really.\"",
                        Grates = "\"It's supposed to be hard. That's the format.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.u.seat", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(in the same seat at the end of the couch) \"Same seat. Same time. You can set a clock by me. Some people find that boring.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I find it reassuring.\"",
                        Lands = "\"Good. Reliable is underrated.\"",
                        Even = "\"Okay. Noted.\"",
                        Grates = "\"You don't have to say that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Some people are right.\"",
                        Lands = "\"Ha. Fine. Boring and still here.\"",
                        Even = "\"Some people are loud. That's not the same as right.\"",
                        Grates = "\"Thanks. I'll move seats, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit in the seat next to it)",
                        Lands = "\"That one's yours now. That's how this works.\"",
                        Even = "\"Okay. Hi.\"",
                        Grates = "\"That seat wobbles. Just so you know.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.u.status", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, Usual = true, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(glancing up from a notebook) \"Status update: nothing new. Everything I said last time still holds. Efficient, right?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Efficient. Anything I should know?\"",
                        Lands = "\"Only that nothing changed. Which is information.\"",
                        Even = "\"No changes. I'd tell you.\"",
                        Grates = "\"If there were, I'd have led with it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll take a boring update from you any day.\"",
                        Lands = "\"Okay. That's nice. Thank you.\"",
                        Even = "\"Okay. Same time next window.\"",
                        Grates = "\"That's a lot of feeling for no update.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"There's always something. Dig.\"",
                        Lands = "\"Fine. The coffee's weaker. That's the news.\"",
                        Even = "\"There isn't. I checked.\"",
                        Grates = "\"There's nothing. Digging won't change that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.bracket", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 4 },
                Open = "(at the kitchen table, sketching a bracket on a napkin) \"I drew a bracket of who's left. My name is still on it, which is new for me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You've earned your spot on it.\"",
                        Lands = "\"I think so too. Saying it out loud feels strange.\"",
                        Even = "\"Maybe. The bracket doesn't care.\"",
                        Grates = "\"Earned is a strong word. Survived.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Where's my name on it?\"",
                        Lands = "\"Near mine. On purpose. Don't tell anyone.\"",
                        Even = "\"Somewhere in the middle. Fluid.\"",
                        Grates = "\"That's classified bracket information.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Did you draw yourself winning?\"",
                        Lands = "\"In pencil. Light pencil.\"",
                        Even = "\"No. Fine, yes. Small.\"",
                        Grates = "\"No. And now I'm erasing the whole thing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.jury", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1, Week = 6 },
                Open = "(watching the empty chairs on a quiet afternoon) \"I've started picturing a jury. People I voted out, sitting in a row, judging me. Is that normal?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Completely normal. And they'll respect you.\"",
                        Lands = "\"That's a good outcome. I'd like that one.\"",
                        Even = "\"Maybe. Some of them.\"",
                        Grates = "\"You can't know that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's normal. Plan for it.\"",
                        Lands = "\"I am. I have a speech. Drafts four through nine.\"",
                        Even = "\"I'm trying. It's hard to model.\"",
                        Grates = "\"I know it's normal. I wasn't asking that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Try not to think about it yet.\"",
                        Lands = "\"Fair. Later. Good advice.\"",
                        Even = "\"I'll try. I'll fail, but I'll try.\"",
                        Grates = "\"Not thinking about things isn't something I can do.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.clock", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(staring at the microwave) \"The microwave clock is four minutes fast. I've told three people. Nobody else seems to care.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I care. Mildly. Fix it.\"",
                        Lands = "\"Thank you. I'll fix it at two a.m. when nobody's watching.\"",
                        Even = "\"I can't fix it. There's no manual. Torture.\"",
                        Grates = "\"Mildly. Great. Nobody cares properly.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Maybe it's right and every other clock is wrong.\"",
                        Lands = "\"That's a terrifying thought. I like it.\"",
                        Even = "\"It's not. I checked against the sun.\"",
                        Grates = "\"It isn't. Please don't do that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Four minutes isn't the end of the world.\"",
                        Lands = "\"It isn't. It's just wrong. Wrong bothers me.\"",
                        Even = "\"No. It's just annoying.\"",
                        Grates = "\"It's four minutes every day. That adds up.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.drill", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(cross-legged on the floor, turning cards face up one at a time) \"Memory drill. Last season's comps had a lot of memory. I'm practicing, just in case.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Quiz me after. Let's see who's faster.\"",
                        Lands = "\"Yes. A benchmark. Ten cards, no talking.\"",
                        Even = "\"Okay. Ten cards. You'll lose.\"",
                        Grates = "\"I don't need a competitor. I need quiet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Drills won't help. Comps are luck.\"",
                        Lands = "\"Some are. The memory ones aren't.\"",
                        Even = "\"Some are. I'm playing the others.\"",
                        Grates = "\"That's wrong, and you said it loudly.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That's smart. Want company?\"",
                        Lands = "\"Sure. Sit. Don't look at the cards.\"",
                        Even = "\"Okay. Quietly.\"",
                        Grates = "\"Company makes me lose count.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.cereal", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(eating cereal dry, one piece at a time) \"I eat the same breakfast every day. People keep offering me eggs. I don't want eggs.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No eggs. Got it.\"",
                        Lands = "\"Thank you. You're the first person who just accepted it.\"",
                        Even = "\"Good. Thank you.\"",
                        Grates = "\"You don't have to make a note of it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"One egg. For science.\"",
                        Lands = "\"Ha. Science doesn't need it. Nice try.\"",
                        Even = "\"No. Science says no.\"",
                        Grates = "\"That's what everyone says. Exactly that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Same breakfast every day sounds boring.\"",
                        Lands = "\"It is. Boring is the point. One less decision.\"",
                        Even = "\"It's efficient. Different word.\"",
                        Grates = "\"It's fine. I didn't ask for a review.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.camera", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(watching a camera lens swivel in the corner) \"That camera follows me when I walk. I've mapped its blind spot. It's about two feet wide.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Show me where it is.\"",
                        Lands = "\"Behind the plant, left side. Don't tell the others.\"",
                        Even = "\"Maybe later. When it's not looking.\"",
                        Grates = "\"Then it wouldn't be a blind spot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You mapped the camera? That's a lot.\"",
                        Lands = "\"It is a lot. I had a slow afternoon.\"",
                        Even = "\"It took ten minutes. Not a lot.\"",
                        Grates = "\"Yes. It is. Thank you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"They'll just move the camera.\"",
                        Lands = "\"Probably. Then I'll map it again.\"",
                        Even = "\"Then I'll remap.\"",
                        Grates = "\"Then I'll remap it. That's not a problem.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "riley.rh.shade", Owner = Owner, Kind = TalkKind.Rhythm, Layer = 0, FromRules = TalkRules.ComesToTalk,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting in a strip of shade in the yard, hood up) \"I'm outside. Voluntarily. People keep pointing it out like it's an event.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's nice to see you out here.\"",
                        Lands = "\"It's nice to be seen out here. I think.\"",
                        Even = "\"It's okay out here. Mostly.\"",
                        Grates = "\"Now you're pointing it out too.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Should we alert the house?\"",
                        Lands = "\"Ha. Please don't. I'd go straight back in.\"",
                        Even = "\"Please don't.\"",
                        Grates = "\"Everyone already did. Fourth time today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit down in the shade without a word)",
                        Lands = "\"That's the best way to do this. Thank you.\"",
                        Even = "\"Okay. The shade's big enough for two.\"",
                        Grates = "\"You can say something. It's weirder if you don't.\"",
                    },
                },
            },
        };
    }
}
