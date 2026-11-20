namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the guarded pool's rhythm talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableGuardedRhythm
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "guarded.rh.slop-label", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(reading the slop bag like a contract) \"There's no ingredient list. It just says 'slop.' That's the whole list.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Ingredient one: regret.\"",
                        Lands = "\"Ingredient two: more regret. I'd add the dates if they let me.\"",
                        Even = "\"Probably. It tastes like it.\"",
                        Grates = "\"I asked a real question. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Does it matter what's in it, if we're eating it?\"",
                        Lands = "\"It matters to me. I like knowing what I'm agreeing to. With food. With people.\"",
                        Even = "\"Not really. I'd still like to know.\"",
                        Grates = "\"That's how people end up eating anything.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Whoever's on slop should read that. It's a nice scare.\"",
                        Lands = "\"Ha. I'll leave it on their pillow. Folded.\"",
                        Even = "\"They've read it. Didn't help.\"",
                        Grates = "\"Why would I scare someone who's already on slop?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.slop-clock", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(eating slop very slowly, a spoon at a time) \"I've timed it. Slop tastes like nothing for nine minutes, then it tastes like a decision.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll sit with you for the ninth minute.\"",
                        Lands = "\"Fine. Don't talk during it. It's a delicate minute.\"",
                        Even = "\"You don't have to. It's just slop.\"",
                        Grates = "\"I'm not sad. I'm eating. There's a difference.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You timed it. Of course you timed it.\"",
                        Lands = "\"Of course I did. You'd have guessed. I don't guess.\"",
                        Even = "\"Somebody had to. Now you know too.\"",
                        Grates = "\"Say it like that again and I'll time how long you're here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Eat it fast. That's the only trick.\"",
                        Lands = "\"Spoken like someone who's never had to. I'll try your way. Once.\"",
                        Even = "\"Fast doesn't work. I checked.\"",
                        Grates = "\"I'll eat it how I eat it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.rule-eleven", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(reading the house rules taped inside a cupboard door) \"Rule eleven. Everyone breaks it every day. I'm waiting to see if anyone ever enforces it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What's rule eleven?\"",
                        Lands = "\"No running by the pool. Watch who runs when they think nobody's looking.\"",
                        Even = "\"No running by the pool. You've broken it twice.\"",
                        Grates = "\"Read it yourself. It's taped right there.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you follow it?\"",
                        Lands = "\"Every day. Nobody's noticed. That's the point of rules.\"",
                        Even = "\"Yes. Somebody has to.\"",
                        Grates = "\"Why? Are you collecting who breaks it?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll start following it, then.\"",
                        Lands = "\"Good. Then there are two of us. Don't make it obvious.\"",
                        Even = "\"Suit yourself. Nobody else does.\"",
                        Grates = "\"Don't do it for me. Do it or don't.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.camera-head", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(very quietly, eyes on the counter) \"I only talk to the cameras in my head. Out loud is for people who want a clip.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You just said that out loud.\"",
                        Lands = "\"To you. That's different. Mostly.\"",
                        Even = "\"Quietly. That counts for something.\"",
                        Grates = "\"You'd make a good story producer.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Smart. The clip's always cut against you.\"",
                        Lands = "\"Always. You'd be surprised who doesn't know that yet.\"",
                        Even = "\"Usually. Not always. I don't gamble on it.\"",
                        Grates = "\"You sound like someone who's planned their clips.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You can say things out loud to me.\"",
                        Lands = "\"I know. I do, sometimes. You've noticed I don't with anyone else.\"",
                        Even = "\"Maybe. Not here.\"",
                        Grates = "\"Everyone says that right before they repeat it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.snore-code", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(bags under their eyes, holding coffee with both hands) \"Someone in the big room snores in a pattern. Four short, one long. I was up decoding it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"And? What's it saying?\"",
                        Lands = "\"'Nominate me.' Every night. I'm taking it under advisement.\"",
                        Even = "\"Nothing good. Mostly vowels.\"",
                        Grates = "\"Please don't be awake right now. It's too early for you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Tell me who. I'll move their pillow.\"",
                        Lands = "\"No. Then I'd lose my hobby. But I'd like to watch you try.\"",
                        Even = "\"You'd pick the wrong one.\"",
                        Grates = "\"I didn't ask for a fixer. I asked for sympathy. Barely.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Here. Have the rest of the coffee.\"",
                        Lands = "\"Thank you. You saw the eyes. Nobody else did.\"",
                        Even = "\"I've had three. But fine.\"",
                        Grates = "\"I didn't say I was dying. I said I was tired.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.shower-clock", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(tapping the bathroom tiles with two fingers, counting) \"Four minutes. That's my shower. Somebody took eleven this morning. I know who.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Was it me?\"",
                        Lands = "\"No. You were six. Six is fine.\"",
                        Even = "\"No. You're a six-minute person.\"",
                        Grates = "\"If you have to ask, it might have been.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're going to tell them, aren't you?\"",
                        Lands = "\"No. I'll just be in there first tomorrow. At six.\"",
                        Even = "\"Not tell. Mention. Once.\"",
                        Grates = "\"I don't tell on people. I just know things.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll go after you tomorrow. Four minutes, promise.\"",
                        Lands = "\"Four. I'll be counting. Thank you, though.\"",
                        Even = "\"I'll hold you to the four.\"",
                        Grates = "\"Don't promise. You'll take five and I'll know.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.shade", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(in the one strip of shade in the yard) \"This is my shade. I got here early. There's a system.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What does a spot in the system cost?\"",
                        Lands = "\"Quiet. You pay in quiet. You can afford it.\"",
                        Even = "\"More than you've got.\"",
                        Grates = "\"It's shade. Not a deal.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "(sit down in it anyway)",
                        Lands = "\"Bold. Fine. You stay on that edge. Don't move with the sun.\"",
                        Even = "\"That's my edge. Whatever.\"",
                        Grates = "\"No. Up. You don't take shade, you ask.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll trade you a cold drink for half a meter.\"",
                        Lands = "\"Deal. A full meter if it's actually cold.\"",
                        Even = "\"Quarter meter. Final offer.\"",
                        Grates = "\"Shade isn't for sale on a hot day. Everyone knows that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.thermometer", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(fanning themselves with a folded menu) \"The yard thermometer is lying. It's hotter. I'd know. I've been checking it hourly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Why do you check it hourly?\"",
                        Lands = "\"So I know when something's lying to me. Practice.\"",
                        Even = "\"Because it's there. And it's wrong.\"",
                        Grates = "\"Because I like to. Is that a problem?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's hot. That's all it has to be.\"",
                        Lands = "\"Fair. I can't leave a number alone. Even the yard's.\"",
                        Even = "\"It's hotter than it says. Just so you know.\"",
                        Grates = "\"You'd say that about anything.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Let's go in. I'll check it for you.\"",
                        Lands = "\"No, I trust my own reading. Come in, though.\"",
                        Even = "\"It'll say the same thing.\"",
                        Grates = "\"Don't do things for me. Not the thermometer.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.diary-fine", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(just out of the diary room) \"They asked how I feel. I said 'fine' four ways. They wanted a fifth.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Try 'fine' with a sigh.\"",
                        Lands = "\"Did that one. Got a laugh behind the glass. I think.\"",
                        Even = "\"Did that one. Nothing.\"",
                        Grates = "\"I'm not workshopping 'fine' with you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Are you fine?\"",
                        Lands = "\"Mostly. That's five. I'll give you that one, not them.\"",
                        Even = "\"Yes. That's the sixth way.\"",
                        Grates = "\"That's exactly what they asked. In that voice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Give them something small. They'll stop digging.\"",
                        Lands = "\"Already learned that. You learn it too. I'll watch.\"",
                        Even = "\"They never stop digging.\"",
                        Grates = "\"I'm not handing them pieces of me for peace.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.diary-rehearse", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(low, by the bathroom) \"Do you rehearse before the diary room? People rehearse in here. I can hear it through the door.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Yes. Out loud. In the shower.\"",
                        Lands = "\"Honest. Me too. In my head. That's the only difference.\"",
                        Even = "\"Everyone does. Most do it badly.\"",
                        Grates = "\"Then I've heard you. Twice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Who's rehearsing? Tell me.\"",
                        Lands = "\"No. But I'll tell you what I learned from it. Later.\"",
                        Even = "\"Doesn't matter who. What matters is what.\"",
                        Grates = "\"So you can use it? No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll knock first, then.\"",
                        Lands = "\"Thank you. Someone in here should.\"",
                        Even = "\"It won't help. They'll hear you knock.\"",
                        Grates = "\"I wasn't asking you to be polite. I was telling you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.bird", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(looking up at the netting over the yard) \"A bird landed on the netting this morning. The whole house went out to look. Best guest we've had.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I saw. It looked right at you.\"",
                        Lands = "\"It did. I'm choosing to take that as a vote.\"",
                        Even = "\"It looked at everyone. The bird's a diplomat.\"",
                        Grates = "\"Don't make the bird about me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Everyone went out. Who stayed in?\"",
                        Lands = "\"Good question. Two people. Nobody's ever asked me that.\"",
                        Even = "\"Two people. Make of it what you like.\"",
                        Grates = "\"I was watching the bird. Unlike some people.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"It was a pigeon. Calm down.\"",
                        Lands = "\"It was. A pigeon with nerve. Leave me this.\"",
                        Even = "\"A pigeon. On a good day, that's news in here.\"",
                        Grates = "\"Fine. Then nothing out there is interesting.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.pencil", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(sharpening a pencil stub on the edge of the counter) \"One pencil in the whole house. I found it. Everyone wants to write something down. Nobody asks who has it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So you're the one who has it.\"",
                        Lands = "\"Me. Now you know. That makes two of us.\"",
                        Even = "\"Me. Keep that to yourself.\"",
                        Grates = "\"Lower your voice. It's a pencil, not a bomb.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I won't tell anyone where it is.\"",
                        Lands = "\"Thank you. It's not about the pencil. It's about knowing.\"",
                        Even = "\"Good. It goes back under the mattress.\"",
                        Grates = "\"You'd tell. Everyone tells eventually.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Lend it. I've got something to write.\"",
                        Lands = "\"Five minutes. Give it back sharp.\"",
                        Even = "\"Write small. It's half a pencil.\"",
                        Grates = "\"No. You'd write about me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.comp-meal", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(eating exactly the same breakfast as yesterday) \"I eat this before every competition. It has never once worked.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Then today's the day.\"",
                        Lands = "\"Statistically, yes. That's the only reason I'm still eating it.\"",
                        Even = "\"Sure. Same as yesterday.\"",
                        Grates = "\"Statistically, no.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"So change it. See if your luck does.\"",
                        Lands = "\"I'd rather keep the losing variable I know.\"",
                        Even = "\"Can't. That's the whole point.\"",
                        Grates = "\"You can't fix a superstition with logic. I've tried.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It'll work today. I can feel it.\"",
                        Lands = "\"You can't. But thank you for lying so confidently.\"",
                        Even = "\"We'll see. By lunch.\"",
                        Grates = "\"Don't. If it fails now it's your fault.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.comp-memory", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(looking at the house clock) \"I memorize the house before every quiz. That clock stopped for a whole day. Did you notice?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"No. Should I have?\"",
                        Lands = "\"Yes. I'll tell you the next thing I notice. One thing.\"",
                        Even = "\"Probably. Most people don't.\"",
                        Grates = "\"No. Forget I said it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You'd have told everyone if it was useful.\"",
                        Lands = "\"I'd have told nobody. I'm telling you.\"",
                        Even = "\"Maybe. Maybe not.\"",
                        Grates = "\"Then you don't know me at all yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Quiz me. Right now.\"",
                        Lands = "\"Color of the hallway rug. Three, two... wrong. Better than most, though.\"",
                        Even = "\"Fine. You'd lose.\"",
                        Grates = "\"I'm not your study partner.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.quiet-day", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(looking around the empty kitchen) \"It's quiet today. Nobody's fighting. Something's being planned.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Or people are just tired.\"",
                        Lands = "\"Maybe. You'd think that. I like that you'd think that.\"",
                        Even = "\"Maybe. I don't believe it, though.\"",
                        Grates = "\"Nobody in here is just tired.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's missing from the kitchen?\"",
                        Lands = "\"Good question. Three people. Same three as Tuesday.\"",
                        Even = "\"A few people.\"",
                        Grates = "\"I'm not your lookout.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I planned it. The quiet. You're welcome.\"",
                        Lands = "\"Liar. But thank you. I'll take it.\"",
                        Even = "\"Sure you did.\"",
                        Grates = "\"Don't joke about plans today.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.books", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(straightening the house bookshelf, one book at a time) \"Eleven books. I've read nine. The other two are the same book, and somebody's underlining it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What are they underlining?\"",
                        Lands = "\"Sentences about trust. Somebody's working something out.\"",
                        Even = "\"Odd lines. I'm keeping track.\"",
                        Grates = "\"If I told you, you'd go and read them.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(take a book and read beside them)",
                        Lands = "\"Good. That's the right way to keep me company. Quietly.\"",
                        Even = "\"Page forty is better than page one.\"",
                        Grates = "\"Not that one. That's the underlined one. Evidence.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You've read nine books in here. Of course you have.\"",
                        Lands = "\"Twice each. You'd be surprised what a third read finds.\"",
                        Even = "\"Nine. Ten by Friday.\"",
                        Grates = "\"Better than talking to people who don't read.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.pepper", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(tasting from a spoon, frowning) \"Someone keeps putting pepper in everything. I've narrowed it to three people.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"It's me. I'm one of the three.\"",
                        Lands = "\"I know. I was waiting to see if you'd say. Good.\"",
                        Even = "\"I know. Twice.\"",
                        Grates = "\"I know. You should've stopped the second time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Are you building a case?\"",
                        Lands = "\"A small one. With exhibits.\"",
                        Even = "\"A small one. Growing.\"",
                        Grates = "\"It's pepper. Not a case. You make everything a case.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Make the next pot. No pepper. I'll guard it.\"",
                        Lands = "\"Deal. You stand there. Look stern.\"",
                        Even = "\"Fine. No pepper. I'll watch the jar.\"",
                        Grates = "\"I don't need a bodyguard for soup.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.bottles", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(reading the back of a shampoo bottle) \"I've read every bottle in the bathroom. Twice. Ask me about conditioner.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Tell me about conditioner.\"",
                        Lands = "\"It has a chapter. I'll spare you. I'll tell you the twist.\"",
                        Even = "\"It's not as interesting as you'd hope.\"",
                        Grates = "\"I wasn't serious.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You're bored out of your mind.\"",
                        Lands = "\"Completely. You're the only one who's said it. Thank you.\"",
                        Even = "\"Yes. Ask me about shampoo too.\"",
                        Grates = "\"Everybody is. That's not news.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Bored people make moves. Be careful.\"",
                        Lands = "\"I know. That's why I'm reading bottles.\"",
                        Even = "\"I'm careful.\"",
                        Grates = "\"Is that a warning or a pitch?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.lockdown", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(lying on the floor of the living room) \"Lockdown for the yard build. Four hours. I've learned every noise this house makes. Ask me what the fridge says.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're going to tell me anyway.\"",
                        Lands = "\"I am. It hums, then clicks every eleven minutes. You're welcome.\"",
                        Even = "\"It clicks. Every eleven minutes. Wait for it.\"",
                        Grates = "\"Then I won't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(lie down on the floor beside them)",
                        Lands = "\"Good. Listen. The next click's in a minute.\"",
                        Even = "\"There's room. Don't breathe loud.\"",
                        Grates = "\"That's my listening spot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd rather hear the fridge than anyone's gossip.\"",
                        Lands = "\"That's the nicest thing anyone's said today. Click. There.\"",
                        Even = "\"Fair. The fridge is more honest.\"",
                        Grates = "\"Don't. It's a fridge. It's not a compliment.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.collar", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(straightening a collar, then dropping their hands) \"I fixed my collar and three people looked. In here even fixing your collar is a move.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's a nice collar.\"",
                        Lands = "\"Thank you. That's not the point. But thank you.\"",
                        Even = "\"It's a collar.\"",
                        Grates = "\"Don't. Now four people are looking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Do it again. See who looks.\"",
                        Lands = "\"Huh. Good idea. Watch the couch.\"",
                        Even = "\"I don't need to. I know who'd look.\"",
                        Grates = "\"So I perform for them? No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who were the three?\"",
                        Lands = "\"You'd ask that. That's why I told you.\"",
                        Even = "\"The usual three.\"",
                        Grates = "\"No. You only wanted the list.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.noticed", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(drawing a small square in the condensation on a glass) \"I play a game. Every day I find one thing in here nobody else has noticed. Want today's?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Always.\"",
                        Lands = "\"Somebody's moving the yard chairs an inch a day toward the door. Watch.\"",
                        Even = "\"There's a new crack in the third tile. Riveting, I know.\"",
                        Grates = "\"You said that too fast. Tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Keep it. It's yours.\"",
                        Lands = "\"That's why you get it. Somebody's started hiding the good knife.\"",
                        Even = "\"Fine. It stays mine. It was a good one.\"",
                        Grates = "\"Then you'll never know. Your loss.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is it me? Did you notice me?\"",
                        Lands = "\"You noticed me noticing. That's tomorrow's.\"",
                        Even = "\"No. You were yesterday's.\"",
                        Grates = "\"Don't fish. It was a tile.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.doormat", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(stepping over the diary room doormat, not on it) \"I don't step on that mat. Don't ask. It's working so far.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What counts as working?\"",
                        Lands = "\"I'm still here. That's the only metric.\"",
                        Even = "\"I'm still here.\"",
                        Grates = "\"You'd want proof. You won't get any.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're the last person I'd take for superstitious.\"",
                        Lands = "\"That's why I do it. Nobody would guess. Except you, now.\"",
                        Even = "\"Everyone has one.\"",
                        Grates = "\"And you're the first person to say it out loud.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "(step over it too, very carefully)",
                        Lands = "\"Good. Now we're both in. Don't tell anyone.\"",
                        Even = "\"You're doing it wrong. Left foot.\"",
                        Grates = "\"Don't copy it. It's mine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.story", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(in the doorway, listening to laughter from the yard) \"They're laughing at a story I heard yesterday. The ending's different today. I'm deciding whether to say.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Say it. Out loud. Fix the ending.\"",
                        Lands = "\"Ha. And be the one who ruins it? Tempting. Not yet.\"",
                        Even = "\"Not yet. An ending that changes is useful.\"",
                        Grates = "\"And make it my story? No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Which ending's true?\"",
                        Lands = "\"Neither. That's the interesting part.\"",
                        Even = "\"The first one. Usually.\"",
                        Grates = "\"If I knew, I'd have said.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Let them have it.\"",
                        Lands = "\"Yes. Let them. I'll just remember both.\"",
                        Even = "\"Probably right. I'll keep both endings.\"",
                        Grates = "\"Easy for you. It's not your story they're bending.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.laundry", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(holding a stack of folded laundry) \"Someone folded my clothes. Wrong. Neatly wrong. I don't know how I feel.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Grateful, then annoyed. In that order.\"",
                        Lands = "\"Annoyed, then grateful. You've got the order wrong.\"",
                        Even = "\"Something like that.\"",
                        Grates = "\"Don't tell me what I feel.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"It's a message. Someone wants you to owe them.\"",
                        Lands = "\"Yes. I'd thought that. I'll unfold them and fold them back. Message received.\"",
                        Even = "\"Maybe. I'll refold them and see who notices.\"",
                        Grates = "\"Not everything's a message. Some of it's laundry.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Somebody did something nice for you. Let it be nice.\"",
                        Lands = "\"That's harder than you'd think. All right. I'll try.\"",
                        Even = "\"Maybe. I'll try nice. Once.\"",
                        Grates = "\"Nice is how they start.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.hoh-stairs", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(watching the new Head of Household climb the stairs with the key) \"Every week someone goes up those stairs one person and comes down another.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who are they coming down as?\"",
                        Lands = "\"Watch the first dinner. I'll tell you what I see. If you'll tell me.\"",
                        Even = "\"Too soon to say.\"",
                        Grates = "\"Ask them. Not me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Would you change, up there?\"",
                        Lands = "\"Yes. I'd lock the door. That's all I'd do.\"",
                        Even = "\"Everyone changes.\"",
                        Grates = "\"That's not a fair question.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You'd come down the same. I'd bet on it.\"",
                        Lands = "\"Would you? That's a strange thing to be sure of. Thank you.\"",
                        Even = "\"Maybe. I'd like to think so.\"",
                        Grates = "\"Don't bet on me. That's how I get nominated.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.hoh-hugs", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(watching the new HoH hug people at the foot of the stairs) \"The new HoH has hugged everyone but two people. I'm one. I'm deciding if that's good.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go and get your hug.\"",
                        Lands = "\"Ha. No. Unhugged is a position. I'm keeping it.\"",
                        Even = "\"Not a chance. I'd rather know why.\"",
                        Grates = "\"And look desperate? No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Who's the other one?\"",
                        Lands = "\"Guess. You'll guess right. That's the worrying part.\"",
                        Even = "\"Not saying. Watch the stairs.\"",
                        Grates = "\"Why? So you can go and count too?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"It's good. Nobody targets who they forgot.\"",
                        Lands = "\"Maybe. Forgotten is safe. Remembered is a plan.\"",
                        Even = "\"Could be. Could be the opposite.\"",
                        Grates = "\"Nobody forgets in here. That's the problem.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.hoh-basket", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(glancing up at the HoH room door) \"There's a snack basket up there. I'm not hungry. I'm counting who gets asked up to eat it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're hungry. And you're counting.\"",
                        Lands = "\"Both. Fine. Both.\"",
                        Even = "\"A little hungry.\"",
                        Grates = "\"Don't tell me what I am.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Have you been asked up?\"",
                        Lands = "\"Not yet. I'd go. I wouldn't eat anything.\"",
                        Even = "\"Not yet. I'm not holding my breath.\"",
                        Grates = "\"That's a strange thing to ask in the kitchen.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"If you get asked up, take one thing. Make them remember they asked.\"",
                        Lands = "\"That's... actually good. Annoyingly good.\"",
                        Even = "\"I don't play with snacks.\"",
                        Grates = "\"Don't coach me. You're not up there either.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.noms-chairs", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(the nomination chairs still out in the living room) \"Nobody's moved the chairs. Nobody wants to be the one who touches them.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll move them.\"",
                        Lands = "\"Go on, then. I'll watch who flinches.\"",
                        Even = "\"Someone will.\"",
                        Grates = "\"Leave them. Moving them says something.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Leave them. They'll do it in the morning.\"",
                        Lands = "\"Yes. Good. Leave them.\"",
                        Even = "\"They always do.\"",
                        Grates = "\"No one will. They'll sit there all week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Are you all right? It was a long ceremony.\"",
                        Lands = "\"I'm fine. Thank you. Ask me again tomorrow.\"",
                        Even = "\"Fine. Tired. That's all.\"",
                        Grates = "\"I'm not on the block. Ask them.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.noms-polite", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(watching the two nominees pass the salt) \"The two on the block are being very polite to each other. That's worse than fighting.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"One of them's already campaigning against the other.\"",
                        Lands = "\"Yes. The one saying 'thank you' too much. I'm glad you saw it.\"",
                        Even = "\"Probably. Watch the salt.\"",
                        Grates = "\"Don't play their game for them out loud.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why is it worse?\"",
                        Lands = "\"Fighting ends. Polite means somebody's waiting.\"",
                        Even = "\"It just is. Watch dinner.\"",
                        Grates = "\"If you need it explained, I'm not explaining it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Pass the salt politely, then. Watch them panic.\"",
                        Lands = "\"Ha. No. But I'll be thinking about it all dinner.\"",
                        Even = "\"No. Tempting, but no.\"",
                        Grates = "\"Not tonight. Read the room.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.noms-friends", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(two cups in hand, both offered to them by other people) \"Everyone's suddenly my friend. Nominations do that. Did they do it to you?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're keeping one of those cups. Whose?\"",
                        Lands = "\"Neither. I'm giving them both to you. Now everyone's confused.\"",
                        Even = "\"Neither. They can wonder.\"",
                        Grates = "\"None of your business. That's the answer.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No one brought me a cup.\"",
                        Lands = "\"Then have one. I wasn't drinking it.\"",
                        Even = "\"Lucky you.\"",
                        Grates = "\"Then don't complain to me about it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm not your friend because of nominations.\"",
                        Lands = "\"I know. That's why you didn't bring a cup.\"",
                        Even = "\"Good. Then you can sit here.\"",
                        Grates = "\"That's what someone would say tonight.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.veto-medal", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(nodding at the veto medal left on the counter) \"Someone took it off to shower. Look. Everyone's walking around it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll put it on. See who notices.\"",
                        Lands = "\"Everyone. Three seconds. Do it. No, don't. Maybe.\"",
                        Even = "\"Everyone would.\"",
                        Grates = "\"That's how you get nominated in a hurry.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why does nobody touch it?\"",
                        Lands = "\"Because it's not ours. In here, that matters more than outside.\"",
                        Even = "\"It's not ours.\"",
                        Grates = "\"You'd touch it, wouldn't you?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Pick it up and give it back. Be the decent one.\"",
                        Lands = "\"Fine. You watch who thanks me and who looks annoyed.\"",
                        Even = "\"Not my job. Not my medal.\"",
                        Grates = "\"Then you'd be the one touching it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.veto-fallout", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(watching people drift out of the living room) \"The meeting took four minutes. The fallout will take four days.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's it landing on?\"",
                        Lands = "\"The person who smiled. They always smile.\"",
                        Even = "\"We'll see. Watch who's smiling.\"",
                        Grates = "\"Don't start the four days early.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You've already got a list.\"",
                        Lands = "\"A short one. You're not on it.\"",
                        Even = "\"Short. Getting shorter.\"",
                        Grates = "\"I've got eyes. That's not a list.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Let's stay out of it.\"",
                        Lands = "\"Yes. Out of it. Kitchen.\"",
                        Even = "\"If they let us.\"",
                        Grates = "\"You can't stay out of it. Nobody can.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.veto-shocked", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(dry, under their breath) \"Someone's said 'shocked' twice today. Nobody in here is shocked by a veto.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'm shocked. I'm shocked you counted.\"",
                        Lands = "\"You're not. But that's funny. I'll allow it.\"",
                        Even = "\"Of course I counted.\"",
                        Grates = "\"Don't. I'm not in the mood today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"They're scared. 'Shocked' is easier.\"",
                        Lands = "\"That's kinder than I was going to be. You might be right.\"",
                        Even = "\"Maybe. Scared people vote early.\"",
                        Grates = "\"Being scared doesn't make it true.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"It means they knew. Who said it?\"",
                        Lands = "\"The one who's going to vote first. Watch.\"",
                        Even = "\"Doesn't matter who. It matters that it's a lie.\"",
                        Grates = "\"If I tell you, you'll use it. So no.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.evict-count", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(rinsing pencil marks off the back of their hand) \"I write the vote count on my hand every eviction. Then I wash it off so nobody sees me keep it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Did the count match what you expected?\"",
                        Lands = "\"One off. One. So somebody lied to me. I'll find out who.\"",
                        Even = "\"Close. Not exact.\"",
                        Grates = "\"That's my count. You get your own.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You don't have to keep the counts alone.\"",
                        Lands = "\"Somebody has to. But. Thank you. Sit.\"",
                        Even = "\"I know. I do it anyway.\"",
                        Grates = "\"I'm not doing it alone. I'm doing it privately.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're not washing it off. You've memorized it.\"",
                        Lands = "\"Of course. The washing is for show. You noticed.\"",
                        Even = "\"Mostly. Some weeks I'd rather forget.\"",
                        Grates = "\"Then you know what I've got. Drop it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.evict-brush", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(at the bathroom sink, moving toothbrushes around in the cup) \"One less. I moved mine so the gap doesn't show.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(move yours over too)",
                        Lands = "\"There. You'd never know. Thank you.\"",
                        Even = "\"Fine. Close enough.\"",
                        Grates = "\"You moved it wrong. Now there's a gap.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Are you running the toothbrush cup now?\"",
                        Lands = "\"Someone has to. It's the only ruling body in here I trust.\"",
                        Even = "\"Yes. Elections are on Thursdays.\"",
                        Grates = "\"Not funny tonight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You hid the gap for you, not the house.\"",
                        Lands = "\"Yes. Who else would I do it for?\"",
                        Even = "\"Maybe. Both.\"",
                        Grates = "\"You didn't have to say it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.evict-meal", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(standing over the table after an eviction, nobody sitting yet) \"First meal after a vote. Nobody wants to sit in the wrong chair.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Their chair. Somebody's going to take it.\"",
                        Lands = "\"Yes. And that tells you who thinks it's their week.\"",
                        Even = "\"Somebody always does.\"",
                        Grates = "\"That's a cold thing to say over the soup.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "(sit in it)",
                        Lands = "\"Huh. Brave. Or dense. I'll decide later.\"",
                        Even = "\"Bold. We'll see if it holds.\"",
                        Grates = "\"Get up. Not tonight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Just sit next to me.\"",
                        Lands = "\"All right. Middle of the table. Nobody can say anything.\"",
                        Even = "\"Fine. Middle seats.\"",
                        Grates = "\"Not if it's a statement.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.usual-again", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(passing in the hall, not stopping) \"You again.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Me again.\"",
                        Lands = "\"Good. Keep walking.\"",
                        Even = "\"Mm. You again, too.\"",
                        Grates = "\"Every time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You don't have to stop.\"",
                        Lands = "\"I wasn't going to. I did anyway.\"",
                        Even = "\"I know.\"",
                        Grates = "\"I wasn't stopping for you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(nod and keep walking)",
                        Lands = "\"That's the right answer.\"",
                        Even = "\"Mm. Later.\"",
                        Grates = "\"Rude. Mostly fine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.usual-sit", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting down next to you, saying nothing for a while) \"This is fine. Isn't it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's fine.\"",
                        Lands = "\"Good. Then we don't have to talk.\"",
                        Even = "\"Fine is plenty.\"",
                        Grates = "\"Don't say it like that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(say nothing back)",
                        Lands = "\"Exactly. That's the whole talk.\"",
                        Even = "\"Mm-hm.\"",
                        Grates = "\"You can say something.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Better than fine.\"",
                        Lands = "\"Don't push it. It's fine.\"",
                        Even = "\"Fine is fine.\"",
                        Grates = "\"Now you've made it a thing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.usual-coffee", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(holding up the pot) \"There's half a cup. Is it yours or mine?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Yours. You made it.\"",
                        Lands = "\"Split it. Fair's fair.\"",
                        Even = "\"Mine, then.\"",
                        Grates = "\"I didn't ask who made it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Yours. I'll make more.\"",
                        Lands = "\"Make it stronger than last time.\"",
                        Even = "\"Thanks. I'll leave you the pot.\"",
                        Grates = "\"Don't. You'll make it weak.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Mine. You had the last one.\"",
                        Lands = "\"True. You keep track too. Good.\"",
                        Even = "\"Fine. Yours. This once.\"",
                        Grates = "\"I did not.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.usual-chair", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(already in a chair, nodding at the one beside it) \"Same chair as yesterday. It's yours, I suppose.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit)",
                        Lands = "\"Good. Same as yesterday.\"",
                        Even = "\"There.\"",
                        Grates = "\"You could've asked.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'm thinking of having it engraved.\"",
                        Lands = "\"Don't. They'll put it on the memory wall.\"",
                        Even = "\"Please don't.\"",
                        Grates = "\"It's a chair.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"People will start to notice.\"",
                        Lands = "\"Let them. It's a chair.\"",
                        Even = "\"Probably. Let them.\"",
                        Grates = "\"Then sit somewhere else.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.usual-news", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(looking up from a book they've read before) \"Page ninety again. Tell me something, or let me read.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Nothing worth a page.\"",
                        Lands = "\"Nothing. Good. Back to ninety.\"",
                        Even = "\"Nothing. As usual.\"",
                        Grates = "\"Of course nothing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You'd know before I did.\"",
                        Lands = "\"True. Nothing.\"",
                        Even = "\"Probably. Still nothing.\"",
                        Grates = "\"That's not an answer.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Something. Big. No, nothing.\"",
                        Lands = "\"Liar. Good. I'll take nothing.\"",
                        Even = "\"Thought so.\"",
                        Grates = "\"Don't do that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rh.usual-day", Pool = TalkPools.Guarded, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting on the back step, squinting at nothing) \"What day is it?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Doesn't matter. Somebody else will count.\"",
                        Lands = "\"That somebody's usually me. Not today.\"",
                        Even = "\"True. It's somebody's job.\"",
                        Grates = "\"It matters.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"A good one. We're both still here.\"",
                        Lands = "\"That'll do.\"",
                        Even = "\"Still here.\"",
                        Grates = "\"Don't jinx it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Our day.\"",
                        Lands = "\"Hm. All right. Ours.\"",
                        Even = "\"If you say so.\"",
                        Grates = "\"No. Just a day.\"",
                    },
                },
            },
        };
    }
}
