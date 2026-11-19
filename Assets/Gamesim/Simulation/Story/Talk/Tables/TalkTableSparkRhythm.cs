namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the spark pool's rhythm talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableSparkRhythm
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "spark.r.slop", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(standing a spoon upright in a bowl of slop; it stays up) \"Look. It stands on its own. That's not food, that's furniture.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Give it here. I'll eat it and live.\"",
                        Lands = "\"Hero. Absolute hero. I'll tell everyone you went first.\"",
                        Even = "\"Your funeral. I'll say something nice.\"",
                        Grates = "\"Don't show off over slop. It's embarrassing for both of us.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll stick to water.\"",
                        Lands = "\"Smart. Water's the only thing in here that doesn't judge you.\"",
                        Even = "\"Coward. Wise coward.\"",
                        Grates = "\"You're no fun. I needed a slop buddy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You lost the comp. Stop narrating the slop.\"",
                        Lands = "\"Ha! Fair. I'll narrate it in my head. Louder.\"",
                        Even = "\"I'll stop. Spoon's still standing, though.\"",
                        Grates = "\"Thanks for the reminder. I'd almost forgotten I lost.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.camera", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(freezing as the camera on the wall whirs round) \"It moved. It moved right when I said that. Do you think they heard? They heard.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Then say something worth airing next time.\"",
                        Lands = "\"Oh, I've got material. Just not that one.\"",
                        Even = "\"Noted. Better lines from now on.\"",
                        Grates = "\"Don't coach me. I know how to be on camera.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"They hear everything. Stop acting surprised.\"",
                        Lands = "\"I'm not surprised, I'm performing surprise. Big difference.\"",
                        Even = "\"Right. Still creepy.\"",
                        Grates = "\"Okay. Nobody asked the expert.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Whatever it was, I'm sure it was fine.\"",
                        Lands = "\"It was not fine. But thanks. You're my alibi now.\"",
                        Even = "\"Hope so. If it airs, you were there.\"",
                        Grates = "\"You didn't even hear it. Don't comfort me blind.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.snore", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(bleary, hair everywhere) \"Someone in our room snores like a boat engine. I've been up since three. I've named it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I sleep through anything. Sorry.\"",
                        Lands = "\"Lucky. I hate you a little. Respectfully.\"",
                        Even = "\"Must be nice. Must be so nice.\"",
                        Grates = "\"Great. So I suffer alone. Thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"What did you name it?\"",
                        Lands = "\"The Foghorn. It has a personality now. It has a backstory.\"",
                        Even = "\"The Foghorn. Don't ask.\"",
                        Grates = "\"Why does that matter? I'm dying here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It might be you. Have you checked?\"",
                        Lands = "\"Excuse me. Okay, that's possible. Don't tell anyone.\"",
                        Even = "\"It's not me. Probably not me.\"",
                        Grates = "\"It's not me. And now I'm offended and awake.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.tiles", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(lying flat on the yard tiles) \"It's so hot the tiles are the cool part. I live down here now. Bring me things.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Get up. You'll fry and blame the house.\"",
                        Lands = "\"I will blame the house. Help me up, then.\"",
                        Even = "\"Five more minutes of tile.\"",
                        Grates = "\"I'm not your project. I'm staying down here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Inside's worse. The air's not working.\"",
                        Lands = "\"So we're trapped either way. Tile it is. Join me.\"",
                        Even = "\"Ugh. Then tile wins.\"",
                        Grates = "\"Thanks for ruining the one hope I had.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "(lie down on the next tile over)",
                        Lands = "\"Yes. Tile friends. Best alliance in the house.\"",
                        Even = "\"Okay. It's nice down here, right?\"",
                        Grates = "\"That's my neighbor tile. Get your own row.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.diaryspoon", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(coming out of the diary room, fanning their face) \"I just talked for twenty minutes about a spoon. They'll never use it. I was brilliant.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"They'll use it. I'd watch twenty minutes on a spoon.\"",
                        Lands = "\"You would. That's why I talk to you. Ten minutes on a fork, coming soon.\"",
                        Even = "\"Glad someone would. The spoon deserves it.\"",
                        Grates = "\"Don't humor me. It was about a spoon.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What about the spoon?\"",
                        Lands = "\"Who used it and didn't wash it. It's a scandal. I'm the reporter.\"",
                        Even = "\"Long story. Very petty. Very good.\"",
                        Grates = "\"That's between me and the diary room. Obviously.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Talk about people in there, not spoons. That's what airs.\"",
                        Lands = "\"The spoon is people. It's whose spoon. Keep up.\"",
                        Even = "\"Next time. Maybe.\"",
                        Grates = "\"I didn't ask for a producer. I have several.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.outside", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(staring at the yard wall) \"I'd give a lot for a normal street right now. Traffic. A dog. A stranger being rude to me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I miss being bored somewhere else.\"",
                        Lands = "\"Yes! Bored somewhere with a door. Exactly that.\"",
                        Even = "\"Same. Bored with options.\"",
                        Grates = "\"You get to be bored. I'm suffering. Different.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I can be rude to you. Like a street.\"",
                        Lands = "\"Oh, would you? Do the traffic, too. Make it immersive.\"",
                        Even = "\"Sure. Go on. Be a street.\"",
                        Grates = "\"Not the same. You'd be rude on camera, which is worse.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"A few more weeks. It'll still be there.\"",
                        Lands = "\"You're right. The dog's waiting. A dog. Somewhere.\"",
                        Even = "\"I know. It's the waiting.\"",
                        Grates = "\"Don't calm me down. I'm enjoying being dramatic.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.hands", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(shaking out their hands by the back door) \"There's a comp tomorrow. I can feel it. I always get the hands before a comp.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Shake them at me. I'll catch the nerves.\"",
                        Lands = "\"Here. Catch. Now we're both doomed. Feels better.\"",
                        Even = "\"Okay. Some nerves for you. Free of charge.\"",
                        Grates = "\"Don't make fun. It's a real thing. It's my thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then use them. Shaky hands are fast hands.\"",
                        Lands = "\"Fast hands. I'm stealing that for the diary room.\"",
                        Even = "\"Maybe. We'll see tomorrow.\"",
                        Grates = "\"You don't know my hands.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Nerves lose comps. Sort it out.\"",
                        Lands = "\"Brutal. Correct. Okay, I'm sorting.\"",
                        Even = "\"I'm working on it. Shaking is working on it.\"",
                        Grates = "\"Wow, very calming. I feel great now.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.sixteen", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(surrounded by every pot in the house) \"I said I'd cook for everyone. I didn't count. There are so many of you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Hand me a knife. I'm on onions.\"",
                        Lands = "\"Hired. Cry on the onions, not on me.\"",
                        Even = "\"Fine. Small pieces. Smaller.\"",
                        Grates = "\"Not like that. Give it back. Supervise from over there.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Feed people well and they keep you. Good move.\"",
                        Lands = "\"It's not a move. It's a move a little. Mostly it's rice.\"",
                        Even = "\"Sure. Or they just eat. Stir that.\"",
                        Grates = "\"I'm cooking, not campaigning. Out of my kitchen.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It smells great already.\"",
                        Lands = "\"It's onion and fear, but thank you. You get the first plate.\"",
                        Even = "\"It's mostly onion. Thanks.\"",
                        Grates = "\"Don't lie, it smells like a pot. Just help.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.ceiling", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(on the couch, throwing a pillow at the ceiling) \"Day whatever. I've counted the ceiling tiles twice. I got different numbers.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Bored people talk. You should be listening instead.\"",
                        Lands = "\"Oh, that's sneaky. Okay. Bored but alert.\"",
                        Even = "\"Fine. I'll listen with my eyes closed.\"",
                        Grates = "\"I'm allowed to be bored without a lesson.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Count them a third time. Tiebreaker.\"",
                        Lands = "\"A tiebreaker. You're a genius. Count with me.\"",
                        Even = "\"I could. That's sad. Okay.\"",
                        Grates = "\"That's not help. That's homework.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm bored too. Badly.\"",
                        Lands = "\"Thank you. Bored together is basically a party.\"",
                        Even = "\"Then let's be bored at the same speed.\"",
                        Grates = "\"Don't say it like that. Now I'm bored and sad.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.hoodie", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(pulling a hoodie over their whole head, voice muffled) \"I'm off camera. Inside the hoodie is off camera. Those are the rules.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Fair. I'll talk to the hoodie.\"",
                        Lands = "\"The hoodie appreciates you. The hoodie is a good listener.\"",
                        Even = "\"Hoodie's listening.\"",
                        Grates = "\"Don't humor the hoodie. The hoodie can tell.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"They can still hear you in there.\"",
                        Lands = "\"I know. It's emotionally off camera, not technically.\"",
                        Even = "\"Let me have this.\"",
                        Grates = "\"You just broke the hoodie. I'm coming out.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "(pull your collar up over your nose)",
                        Lands = "\"Yes. Hoodie council. We're in session.\"",
                        Even = "\"Okay. Two of us off camera. Very safe.\"",
                        Grates = "\"Copying isn't off camera. That's just weird.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.knock", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(knocking on the counter three times) \"Don't look at me like that. I knock before every nomination day. It's never failed me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"That's plastic. It doesn't count.\"",
                        Lands = "\"Oh, no. That explains last week. Find me real wood.\"",
                        Even = "\"It counts if you believe. I believe.\"",
                        Grates = "\"Great. Now I'm cursed and you did it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Knock once for me while you're at it.\"",
                        Lands = "\"Done. You're covered. You owe me a knock sometime.\"",
                        Even = "\"One knock. Don't waste it.\"",
                        Grates = "\"It doesn't transfer. Get your own luck.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Luck's for people who don't trust their game.\"",
                        Lands = "\"Or people who trust their game and want backup. Both.\"",
                        Even = "\"I trust my game. I trust wood more.\"",
                        Grates = "\"And you'll be the first one luck forgets. Just saying.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.shower", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(third in line for the shower, towel over one shoulder) \"There's one shower with any pressure, and everyone knows which. This is war.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Go ahead of me. I'm in no rush.\"",
                        Lands = "\"You're an angel. I'll remember this at the vote. Partly kidding.\"",
                        Even = "\"Thanks. I'll be quick. I won't.\"",
                        Grates = "\"No, now I feel bad. I'm staying in line. Ugh.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I hear if you cry, they let you through.\"",
                        Lands = "\"I'm already crying. Watch me cut.\"",
                        Even = "\"Not a bad trick. Hold my towel.\"",
                        Grates = "\"Don't give me ideas. I'll do it and it'll air.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'm going next. Fight me.\"",
                        Lands = "\"Ooh, a challenger. Fine, go. I'll heckle.\"",
                        Even = "\"Bold. Rude. Go on.\"",
                        Grates = "\"Wow. In a queue? Really?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.pan", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(holding up a crusted pan like evidence) \"Who leaves a pan like this? A monster. A monster lives among us.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"That might have been me. Sorry.\"",
                        Lands = "\"You! Honesty! I forgive you. Wash it, but I forgive you.\"",
                        Even = "\"Okay. Wash it and we never speak of it.\"",
                        Grates = "\"And you let me hold it up first. Wow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Name the monster at dinner. Make it a show.\"",
                        Lands = "\"A public trial. I love it. I'm prosecuting.\"",
                        Even = "\"Maybe. I need evidence first.\"",
                        Grates = "\"And make an enemy over a pan? No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Keep it. Somebody will want that forgotten.\"",
                        Lands = "\"Pan blackmail. Sick. It's going under my bed.\"",
                        Even = "\"Hm. Leverage pan. Okay.\"",
                        Grates = "\"It's a pan, not a file. Just wash it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.float", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(on a pool float, drifting into the wall again) \"I can't steer this. I've hit the wall six times. I've accepted it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Want a push? I'll aim you at someone.\"",
                        Lands = "\"Yes. Aim me at whoever's plotting. Bump them.\"",
                        Even = "\"Gently. I'm fragile.\"",
                        Grates = "\"Don't you dare. I'll sink and blame you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Drifting is a strategy too.\"",
                        Lands = "\"Ha. The float game. Nobody sees it coming.\"",
                        Even = "\"That's generous. I'm just drifting.\"",
                        Grates = "\"Not everything's a metaphor. It's a float.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Leave it. The wall's a friend now.\"",
                        Lands = "\"The wall is my closest ally. It never lies.\"",
                        Even = "\"Wall and me, forever.\"",
                        Grates = "\"Don't give up on me. Push me back out.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.lightsout", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting bolt upright as the lights cut out at once) \"They do lights out like a jump scare. Every night. And every night I scream a little.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Scream louder. Make them change it.\"",
                        Lands = "\"A scream protest. Join me tomorrow.\"",
                        Even = "\"Maybe I will. Tomorrow.\"",
                        Grates = "\"And wake the whole room? No thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll count you down next time.\"",
                        Lands = "\"A countdown! That's ridiculous and kind.\"",
                        Even = "\"Okay. From five. Loudly.\"",
                        Grates = "\"Don't baby me. I'll just scream.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"It's every night. You know it's coming.\"",
                        Lands = "\"Knowing doesn't help. That's the horror of it.\"",
                        Even = "\"I know. My body doesn't.\"",
                        Grates = "\"Thanks. Very helpful at midnight.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.whatday", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(to the room, to nobody) \"What day is it? Not the date. The day. I need to know if it's a good day.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Count the comps. It's the day after one.\"",
                        Lands = "\"Oh, that's clever. Comp time. That's how I count now.\"",
                        Even = "\"Comp time. Okay.\"",
                        Grates = "\"I asked the day, not for a system.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Days don't matter in here. Only votes do.\"",
                        Lands = "\"Bleak. True. Very bleak. I love it.\"",
                        Even = "\"Okay, gloomy. Vote day, then.\"",
                        Grates = "\"I just wanted to know what day it was.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's a good day. I've decided.\"",
                        Lands = "\"You can decide that? Great. Good day. Official.\"",
                        Even = "\"Okay. I'll take your word.\"",
                        Grates = "\"You can't just decide. That's not how days work.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.jar", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(holding up a half-empty jar) \"Someone ate my peanut butter. I labeled it. I drew a face on it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Have some of mine. Not as good, but still.\"",
                        Lands = "\"Oh. That's lovely, actually. I'm drawing a face on yours.\"",
                        Even = "\"Okay. Not the same, but okay.\"",
                        Grates = "\"It's not the food. It's the principle. Keep yours.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That face was a work of art.\"",
                        Lands = "\"Thank you! It had a moustache. It had a personality.\"",
                        Even = "\"It was. They ate around it.\"",
                        Grates = "\"Don't mock the face. The face was serious.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Labels never work in here.\"",
                        Lands = "\"I know. I'm hiding the next one in a shoe.\"",
                        Even = "\"Learning that the hard way.\"",
                        Grates = "\"So it's my fault for labeling? Cool.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.nap", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(waking on the couch with a cushion print on one cheek) \"How long was I out? Did I miss anything? Did anyone talk about me?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Napping on the couch is how you get talked about.\"",
                        Lands = "\"Fair. I'll nap with one eye open from now on.\"",
                        Even = "\"Probably. Fill me in.\"",
                        Grates = "\"Thanks for the scare. I'm awake now. Thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Twenty minutes. Nothing happened.\"",
                        Lands = "\"Thank you. A straight answer. You're my news now.\"",
                        Even = "\"Okay. Boring, but good.\"",
                        Grates = "\"Nothing? You're lying. I can tell.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only that you snore. It was beautiful.\"",
                        Lands = "\"It was not! Was it? Was it beautiful?\"",
                        Even = "\"Rude. Possible.\"",
                        Grates = "\"I don't snore. Take that back, on camera.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.pushups", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(doing push-ups in the yard, counting out loud) \"Thirty. Thirty-one. Talk to me, it helps. Thirty-two.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're doing great. Keep going.\"",
                        Lands = "\"Thanks, coach. Thirty-three. That's the new record.\"",
                        Even = "\"Thanks. Thirty-three. Ow.\"",
                        Grates = "\"Don't cheer. It makes me lose count.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why push-ups now? It's boiling.\"",
                        Lands = "\"I'm bored and there's a comp coming. Thirty-four.\"",
                        Even = "\"No reason. Thirty-four. Good question.\"",
                        Grates = "\"Because I want to. Thirty. I lost count.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Training where everyone can see. Smart.\"",
                        Lands = "\"Ha. You caught that. Thirty-five. Message sent.\"",
                        Even = "\"Maybe. Thirty-five.\"",
                        Grates = "\"I'm exercising. Not everything's a message.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.soak", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(in the hot tub, only a head above the water) \"I've been in here so long I'm part soup. Do not tell me to get out.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You look like a raisin.\"",
                        Lands = "\"A happy raisin. Still a raisin. Fine.\"",
                        Even = "\"Raisins are healthy.\"",
                        Grates = "\"Thanks. I'm getting out now, so you win.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Soup needs more ingredients. Room for one?\"",
                        Lands = "\"Get in. We're a stew now.\"",
                        Even = "\"Squeeze in. Don't splash.\"",
                        Grates = "\"No. This is a soup for one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I won't. Stay as long as you like.\"",
                        Lands = "\"Thank you. You're the only one who respects soup.\"",
                        Even = "\"I will. Forever.\"",
                        Grates = "\"You're no fun. Somebody should tell me to get out.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.shirts", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(holding up two shirts that look exactly the same) \"Which one? This is important. This is a nomination-day shirt.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"The left one. It's got more confidence.\"",
                        Lands = "\"It does. The left one's a winner.\"",
                        Even = "\"Left. Okay. Same shirt, but okay.\"",
                        Grates = "\"You didn't even look. I can tell.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Neither. Wear the loud one. Make them look.\"",
                        Lands = "\"Ooh. Nobody nominates a loud shirt.\"",
                        Even = "\"Bold. I'll think about it.\"",
                        Grates = "\"And be a target? Easy for you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Nobody's ever been saved by a shirt.\"",
                        Lands = "\"Because nobody's tried this shirt. Watch.\"",
                        Even = "\"Okay, but what if?\"",
                        Grates = "\"You must be a joy to shop with.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.wakeup", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(groaning into a pillow as the wake-up music blares) \"Why is it always this song? What did we do to deserve this song?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Sing along. Loud. Ruin it for them.\"",
                        Lands = "\"Revenge karaoke. Hit the high note.\"",
                        Even = "\"Maybe after coffee.\"",
                        Grates = "\"It's seven. Don't be like that at seven.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"That song means a comp day. Get up first.\"",
                        Lands = "\"You're right, they always play it before a comp. Up. Up.\"",
                        Even = "\"Up. In a minute.\"",
                        Grates = "\"Don't plan at me before coffee.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll make the coffee. You suffer.\"",
                        Lands = "\"Bless you. I'm suffering better already.\"",
                        Even = "\"Thanks. I'll suffer slowly.\"",
                        Grates = "\"Don't be cheerful. Cheerful is too loud.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.window", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(pressed against the one frosted window) \"I think there's a tree out there. Or a person. A tree-shaped person.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Could be a producer. Act natural.\"",
                        Lands = "\"Act natural. Okay. Look how natural I am.\"",
                        Even = "\"Natural. Sure. Always.\"",
                        Grates = "\"Don't make it creepy. It was a nice tree.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Let it be a tree. It's nicer.\"",
                        Lands = "\"Okay. A tree. I'm naming it.\"",
                        Even = "\"Fine. Tree. I'm still naming it.\"",
                        Grates = "\"No. It's a person. I'm sure now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's a tree. I've looked.\"",
                        Lands = "\"Thank you. I'll stop. I'll keep looking, but I'll stop.\"",
                        Even = "\"Okay. Boring. Okay.\"",
                        Grates = "\"You ruin everything with facts.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.cards", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1 },
                Open = "(shuffling a deck with cards missing) \"We're down to forty-one cards. I'm inventing a game for forty-one cards. You're in.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'm in. You explain the rules.\"",
                        Lands = "\"There are no rules yet. You're on the rules committee.\"",
                        Even = "\"Okay. Rule one: I win.\"",
                        Grates = "\"Don't just agree. Argue. Games need arguing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"A game with no rules is just you winning.\"",
                        Lands = "\"Correct. You're learning. Sit down.\"",
                        Even = "\"Fine. You can make one rule.\"",
                        Grates = "\"Then don't play. More cards for me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"The missing cards are wild. All of them.\"",
                        Lands = "\"Missing cards wild! Genius. You're my rules partner.\"",
                        Even = "\"Wild. Okay. Sure.\"",
                        Grates = "\"That makes no sense. Sit out a round.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.hohbed", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(back from the HoH room tour) \"Did you see the bed up there? I'd play this whole game again for that bed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You'll be looking at it from the block if you're not careful.\"",
                        Lands = "\"Dark. True. Fine, I'll sleep in my bunk and dream.\"",
                        Even = "\"Harsh. Fair.\"",
                        Grates = "\"I was talking about a bed.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Next week it's yours. I'll come visit.\"",
                        Lands = "\"Deal. You get the good side. I'll bring snacks.\"",
                        Even = "\"From your lips. Okay.\"",
                        Grates = "\"Don't jinx me with nice things.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Win the next one. Simple.\"",
                        Lands = "\"Simple! Okay. You're my coach now.\"",
                        Even = "\"Easy to say.\"",
                        Grates = "\"Just win. Why didn't I think of that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.hohnice", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(watching the new HoH walk past with the key) \"Everyone's being so nice to the winner. I'm being nice too. I hate how good I am at it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're nice to everyone. It's not an act.\"",
                        Lands = "\"It's a small act. A tiny one. Thank you, though.\"",
                        Even = "\"Mostly not. Today a bit.\"",
                        Grates = "\"It is a bit. Don't let me off.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Bake them something. Go all in.\"",
                        Lands = "\"Shameless. I'm doing it.\"",
                        Even = "\"Too much? Maybe too much.\"",
                        Grates = "\"And look desperate? I have some pride.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Or skip it. Let them come to you.\"",
                        Lands = "\"Play it cool. I have never once played it cool.\"",
                        Even = "\"Maybe. I'm bad at cool.\"",
                        Grates = "\"That's how you end up on the block.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.hohseats", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterHoH },
                Open = "(in the kitchen, hovering by the table) \"New HoH, new week, new seating at dinner. Who do I sit with? Help.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Sit where you always sit.\"",
                        Lands = "\"Normal. Brave. Okay. Normal it is.\"",
                        Even = "\"Nobody will notice. Everyone will notice.\"",
                        Grates = "\"That's not a plan. That's giving up.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Sit next to the HoH. Own it.\"",
                        Lands = "\"Bold. I'll ask about their letter.\"",
                        Even = "\"Too much, maybe.\"",
                        Grates = "\"You want me to look like a suck-up?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Sit with whoever's likely to go up.\"",
                        Lands = "\"Wait, that's smart. Nobody targets the sad table.\"",
                        Even = "\"Morbid. Maybe.\"",
                        Grates = "\"That's cold. I'm sitting with whoever makes me laugh.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.notme", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(folding a nomination-day shirt they never needed) \"Not me this week. I feel guilty about how relieved I am.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do a little dance. Quietly.\"",
                        Lands = "\"A very small dance. In my heart. Okay, and my feet.\"",
                        Even = "\"Later. In the storage room.\"",
                        Grates = "\"Not funny. Two people are on the block.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Don't look relieved. They'll remember.\"",
                        Lands = "\"You're right. Sad face. Like this?\"",
                        Even = "\"I'll try. I'm bad at faces.\"",
                        Grates = "\"Can I feel a feeling for one minute?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"It's okay to be relieved.\"",
                        Lands = "\"Thank you. I'm relieved. Just to you.\"",
                        Even = "\"Is it? Okay.\"",
                        Grates = "\"Don't tell me what's okay. I'll feel bad if I want to.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.pretending", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(on the stairs, watching the two nominees in the yard) \"They're both pretending they're fine. I'm pretending I don't see them pretending.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go talk to them. Somebody should.\"",
                        Lands = "\"You're right. Come with me. I'll do the talking.\"",
                        Even = "\"Later. When they're not pretending.\"",
                        Grates = "\"And say what? You go.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Leave them be for today.\"",
                        Lands = "\"Yeah. Tomorrow. That's kind of you.\"",
                        Even = "\"Okay. Today's theirs.\"",
                        Grates = "\"Easy to say if you never liked them.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Pretending is the game. They'll be fine.\"",
                        Lands = "\"Cold, but yeah. They're good at this. They'll be fine.\"",
                        Even = "\"Maybe. Still hard to watch.\"",
                        Grates = "\"Remind me never to get nominated around you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.speech", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterNoms },
                Open = "(after the ceremony, the house far too quiet) \"Nobody's talking. Should I say something? I always say something. I'm going to say something.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Wait an hour. Say it when it counts.\"",
                        Lands = "\"An hour. I can do an hour. Maybe forty minutes.\"",
                        Even = "\"Fine. Timing. I hate timing.\"",
                        Grates = "\"I don't need a schedule to talk.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Don't. Nobody wants a speech right now.\"",
                        Lands = "\"Ouch. Correct. No speech.\"",
                        Even = "\"Fine. A small one, later.\"",
                        Grates = "\"Wow. Remind me never to ask you for a pep talk.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Say something. You're good at that.\"",
                        Lands = "\"I am. I'll make them laugh. Somebody has to.\"",
                        Even = "\"Okay. Something light.\"",
                        Grates = "\"Don't push me out there. No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.sticky", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(back from the veto comp, covered in something) \"Don't hug me. I'm sticky. I didn't win, but I'm sticky, so that's something.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No hugs. I'll get you a towel.\"",
                        Lands = "\"A towel! A hero. A dry hero.\"",
                        Even = "\"Okay. Big towel.\"",
                        Grates = "\"So you'd hug me, you just won't. Got it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "(hug them anyway)",
                        Lands = "\"Oh no. Now you're sticky. We're sticky together.\"",
                        Even = "\"Okay. Well. Now you know.\"",
                        Grates = "\"I said don't. Now we're both a mess.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What happened out there?\"",
                        Lands = "\"Everything. I'll tell you once I can feel my hands.\"",
                        Even = "\"Slipped. Twice. Don't ask.\"",
                        Grates = "\"Don't. It's still raw. It's still sticky.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.knewit", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(watching the veto holder wear the medal) \"Veto's used or it's not, and everyone pretends they knew all along. I love this house.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Did you know all along?\"",
                        Lands = "\"Obviously. I know nothing and everything. Both.\"",
                        Even = "\"Kind of. Not really.\"",
                        Grates = "\"Don't quiz me. I'm on your side, I think.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I didn't know. I'm still not sure.\"",
                        Lands = "\"Thank you. Someone admitting it. That's huge.\"",
                        Even = "\"Same. Keep it quiet.\"",
                        Grates = "\"Then don't look so calm about it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I knew. I know everything. Ask me.\"",
                        Lands = "\"Okay, oracle. Who wins the season?\"",
                        Even = "\"Sure you do. Who goes home, then?\"",
                        Grates = "\"Not funny today. Somebody's in trouble.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.waiting", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterVeto },
                Open = "(on the edge of the hot tub, feet in) \"Veto meeting's done. The week's decided. The weirdest part is the waiting after.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Wait with me, then.\"",
                        Lands = "\"Good. Waiting's easier with a person.\"",
                        Even = "\"Okay. Feet in. Water's warm.\"",
                        Grates = "\"I didn't ask for company. But fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"The waiting's the worst part.\"",
                        Lands = "\"Yes! Nobody admits that. Thank you.\"",
                        Even = "\"It is. Feet in, at least.\"",
                        Grates = "\"Easy to say when it isn't your week on the line.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Waiting's when votes move. Use it.\"",
                        Lands = "\"Ugh. You're right. Fine. I'll go talk.\"",
                        Even = "\"Maybe. Later.\"",
                        Grates = "\"Can I just put my feet in the water?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.curtains", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(re-hanging the curtains on a bunk nobody sleeps in now) \"Someone has to fix their side. I always do it. Nobody asks me to.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why you, every time?\"",
                        Lands = "\"Because otherwise it's like they're coming back.\"",
                        Even = "\"Somebody has to. Might as well be the nosy one.\"",
                        Grates = "\"Because nobody else bothers. You included, apparently.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Give me a curtain. I'll be useful for once.\"",
                        Lands = "\"You are! Corner first. Good.\"",
                        Even = "\"Okay. Hold this end.\"",
                        Grates = "\"Not a joke right now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll leave you to it.\"",
                        Lands = "\"Thanks. I need ten minutes with this.\"",
                        Even = "\"Okay. Shut the door on your way out?\"",
                        Grates = "\"You could help. Just saying.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.door", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(staring at the front door after it shuts) \"That door is so loud when it closes. You'd think they'd oil it. It's on purpose. It has to be.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"It's the door's big moment. Let it have it.\"",
                        Lands = "\"Drama door. It's earned the spotlight.\"",
                        Even = "\"Sure. Diva door.\"",
                        Grates = "\"Not now. Too soon.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Next week I'm oiling it myself.\"",
                        Lands = "\"Ooh. A rebel. I'll keep watch.\"",
                        Even = "\"They'd stop you.\"",
                        Grates = "\"And get the whole house punished? Sure.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"It's to remind us who's next.\"",
                        Lands = "\"Brutal. Effective. Very much on purpose.\"",
                        Even = "\"Yeah. Probably.\"",
                        Grates = "\"Thanks. I'll sleep great tonight.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.r.icecream", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0,
                When = new TalkGate { Layer = 1, Window = TalkWindows.AfterEviction },
                Open = "(eating ice cream from the tub at midnight) \"Eviction-night food. It doesn't count. Nothing eaten on eviction night counts.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Pass the spoon. Rules are rules.\"",
                        Lands = "\"Yes. Two spoons. It counts even less.\"",
                        Even = "\"One bite. Okay, two.\"",
                        Grates = "\"Get your own tub. This one is grief.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Sharing a tub after a vote builds trust.\"",
                        Lands = "\"Then take a spoon. Let's trust each other a little.\"",
                        Even = "\"Sure. A spoon of trust.\"",
                        Grates = "\"It's ice cream. Not a meeting.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You okay?\"",
                        Lands = "\"Ask me after the tub. I'm getting there.\"",
                        Even = "\"Mostly. The tub is helping.\"",
                        Grates = "\"If I say no, will you leave me with the ice cream?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.u.couch", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(dropping onto the couch next to you) \"Same couch, same time, {player}. We're basically furniture now.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Good furniture, though.\"",
                        Lands = "\"The best. Nobody votes out a couch.\"",
                        Even = "\"Sturdy. Reliable.\"",
                        Grates = "\"You can be furniture. I'm still a person.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Best spot in the house.\"",
                        Lands = "\"Don't say it out loud. They'll take it.\"",
                        Even = "\"It's a good spot.\"",
                        Grates = "\"It's a couch. Calm down.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Nothing new today. You?\"",
                        Lands = "\"Nothing. Isn't it great?\"",
                        Even = "\"Nope. Same.\"",
                        Grates = "\"You could make something up for me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.u.tea", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(holding out a mug) \"Tea. The usual. I don't even ask anymore.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You made it too strong last time.\"",
                        Lands = "\"And you drank it all. Here. Strong again.\"",
                        Even = "\"Noted. Probably still too strong.\"",
                        Grates = "\"Then make your own.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Thanks. I needed that.\"",
                        Lands = "\"I know. I always know.\"",
                        Even = "\"Anytime. Same mug as always.\"",
                        Grates = "\"Don't sound so surprised.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do I get a stamp card? Tenth one free?\"",
                        Lands = "\"This is the tenth. They're all free.\"",
                        Even = "\"No card. Drink it.\"",
                        Grates = "\"Don't push your luck.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.u.stillhere", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(passing you in the hall, not stopping) \"Still here?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"For now. I'm counting the days.\"",
                        Lands = "\"Same. Keep counting. I'm counting too.\"",
                        Even = "\"Good. Stay that way.\"",
                        Grates = "\"Don't count. It's bad luck.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Still here.\"",
                        Lands = "\"Good. Keep it up.\"",
                        Even = "\"Good. Same time tomorrow.\"",
                        Grates = "\"That's it? Two words? Okay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Barely. You?\"",
                        Lands = "\"Barely, together. That's the spirit.\"",
                        Even = "\"Same. Barely.\"",
                        Grates = "\"Don't say barely. It makes me nervous.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.u.sky", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting on the yard step beside you, watching nothing) \"Same sky as yesterday.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I like this sky.\"",
                        Lands = "\"Me too. Good sky.\"",
                        Even = "\"It's a sky. I'll take it.\"",
                        Grates = "\"You like everything today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"It's the only sky we get.\"",
                        Lands = "\"Then I'll enjoy it. Hard.\"",
                        Even = "\"True. Make it last.\"",
                        Grates = "\"Ruin the sky, why don't you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"One cloud moved. Big news.\"",
                        Lands = "\"Breaking news. I'll tell the house.\"",
                        Even = "\"Huge. Massive.\"",
                        Grates = "\"Don't. I was resting.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.u.mugs", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(rinsing the same two mugs as every night) \"You wash, I dry?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"I washed last time. You wash.\"",
                        Lands = "\"Ugh. Correct. Fine.\"",
                        Even = "\"Fine. You keep score.\"",
                        Grates = "\"Keeping a tally on mugs now?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Always.\"",
                        Lands = "\"Best part of the day. Don't tell anyone.\"",
                        Even = "\"Okay. You dry, I gossip.\"",
                        Grates = "\"You don't have to say always.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll do both. Go sit.\"",
                        Lands = "\"Look at you. I'll sit. I'll heckle.\"",
                        Even = "\"Okay. Show-off.\"",
                        Grates = "\"Don't take my job. I like this job.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.u.boring", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Layer = 0, Usual = true,
                When = new TalkGate { Layer = 1 },
                Open = "(stretched out on the floor, yawning) \"Tell me something boring. Help me sleep.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Sleep. I'll keep it down.\"",
                        Lands = "\"You're sweet. Goodnight. Kind of goodnight.\"",
                        Even = "\"Thanks. Okay. Night.\"",
                        Grates = "\"No, I said talk. Talk to me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"There are four cereals and all of them are stale.\"",
                        Lands = "\"Perfect. So boring. I'm already drifting.\"",
                        Even = "\"Okay. That's boring.\"",
                        Grates = "\"Too interesting. Now I want cereal.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"No. Stay up. Something might happen.\"",
                        Lands = "\"Fine. Five more minutes of something.\"",
                        Even = "\"Nothing ever happens. Okay, five minutes.\"",
                        Grates = "\"Nothing will happen. Let me sleep.\"",
                    },
                },
            },
        };
    }
}
