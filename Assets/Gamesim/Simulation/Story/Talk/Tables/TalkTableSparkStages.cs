namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the spark pool's friendship stage talks (slice S2, talk rules 2; ruling R1: the talks are C# tables). Three
    /// rows per stage, Kind = TalkKind.Stage, Stage = Acquainted, Friendly, Friend or CloseFriend; the Close friend talk asks a
    /// favor and its two replies carry TalkReply.Favour (Keep, "I've got you"; Refuse, "Can't promise that"). American spelling.
    /// </summary>
    public static class TalkTableSparkStages
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "spark.stage.acquainted.1", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(bumping your shoulder on the way past the fridge) \"Oh, it's you. See, now I know your face before your name. That's progress.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Took you long enough.\"",
                        Lands = "\"Hey, I'm learning a whole house at once. You made the top of the list.\"",
                        Even = "\"Rude. Accurate, but rude.\"",
                        Grates = "\"Wow. And I was going to share my cereal.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Good. I know yours too.\"",
                        Lands = "\"Look at us. Basically old pals already.\"",
                        Even = "\"Great. Now we just need a handshake.\"",
                        Grates = "\"You say that like it's a chore.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Faces are easy. Names stick later.\"",
                        Lands = "\"Smart. I'll remember yours, I promise. Mostly.\"",
                        Even = "\"Sure. Faces first, then the rest.\"",
                        Grates = "\"Wow, okay, slow down, professor.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.acquainted.2", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(sliding onto the stool next to you) \"You're the one who laughs at the right part of my stories. I noticed. That makes you my breakfast person.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Happy to be your breakfast person.\"",
                        Lands = "\"Excellent. Same stool tomorrow. It's a standing booking.\"",
                        Even = "\"Good. Don't be late.\"",
                        Grates = "\"Okay, you don't have to sound so official.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Your stories could be shorter.\"",
                        Lands = "\"Ha! Honest. Fine, shorter ones, just for you.\"",
                        Even = "\"They could. They won't be.\"",
                        Grates = "\"Ouch. Breakfast person revoked. Temporarily.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I just like a good story.\"",
                        Lands = "\"And I like a good audience. We're set.\"",
                        Even = "\"Then you're in the right kitchen.\"",
                        Grates = "\"Just a story? I put work into those.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.acquainted.3", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(tossing you an orange from across the kitchen) \"Catch. See? I already know you've got good hands. We're past the small talk now.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"That's your whole read on me? Hands?\"",
                        Lands = "\"It's a start! Give me a week and I'll know your snack order too.\"",
                        Even = "\"Hands, and you don't drop things. That's plenty.\"",
                        Grates = "\"Fine. Hands, and no sense of humor.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Good catch, though. Thanks.\"",
                        Lands = "\"See, polite too. I'm adding that to the file.\"",
                        Even = "\"Anytime. The oranges are for everybody.\"",
                        Grates = "\"Wow, all business. Okay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Past small talk is a big step in here.\"",
                        Lands = "\"It is, and I'm taking it. Keep up.\"",
                        Even = "\"Maybe. Medium talk, then.\"",
                        Grates = "\"Relax. It's an orange, not a contract.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.friendly.1", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(stretched out on the yard couch, sunglasses on at night) \"Okay, house secret. Half of them are scared of the kitchen at night. I'm not. Now you know.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Scared of a kitchen?\"",
                        Lands = "\"The fridge hums. It's spooky. You'll understand at three in the morning.\"",
                        Even = "\"Laugh now. Wait till it hums.\"",
                        Grates = "\"Okay, forget I said anything.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Your secret's safe with me.\"",
                        Lands = "\"I knew it would be. That's why I told you.\"",
                        Even = "\"It's not a big secret. But thanks.\"",
                        Grates = "\"It's a joke. You don't have to be so solemn.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Which half?\"",
                        Lands = "\"Ha! Look at you, asking the right question. Later. Over snacks.\"",
                        Even = "\"The half that isn't us.\"",
                        Grates = "\"Easy. I said a house secret, not a list.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.friendly.2", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(whispering over the top of a cereal box) \"Don't tell anyone, but I time the showers. Somebody in here takes forever. Every single day.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You time the showers? That's a lot.\"",
                        Lands = "\"I'm bored and observant. Deadly mix.\"",
                        Even = "\"It's a hobby. Everyone needs one.\"",
                        Grates = "\"Okay, judgy. You'll thank me later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Is it me? Just say it's me.\"",
                        Lands = "\"It's not you! You're quick. I'm impressed, honestly.\"",
                        Even = "\"Not you. Relax.\"",
                        Grates = "\"It wasn't. Now I'm checking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll keep it between us.\"",
                        Lands = "\"That's why you get the inside info.\"",
                        Even = "\"Good. The stopwatch stays secret.\"",
                        Grates = "\"Wow, no reaction at all? Tough crowd.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.friendly.3", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(flopping onto the bunk across from yours) \"You know what? You're the only one in here I don't have to be on for. It's weirdly relaxing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You don't have to perform for me.\"",
                        Lands = "\"See, that's exactly what I mean. Nice.\"",
                        Even = "\"Good. My face needs a break.\"",
                        Grates = "\"Okay, easy. Don't make it a whole speech.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Honestly, I'm a little offended you're not on for me.\"",
                        Lands = "\"Ha! Fine, I'll do one bit. Just one.\"",
                        Even = "\"You get the off-duty version. It's rarer.\"",
                        Grates = "\"See, that's the kind of thing that keeps me on.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Careful. Off-duty is when people say things.\"",
                        Lands = "\"Oh, I know. I'll only say the fun things.\"",
                        Even = "\"True. I'll keep it light.\"",
                        Grates = "\"Wow. And I was just relaxing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.friend.1", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(pulling you aside by the pantry) \"I'm just going to say it. I'm really glad you're in here. Like, genuinely. Okay, that's all.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad you're in here too.\"",
                        Lands = "\"Stop, I'll get emotional on camera. Too late.\"",
                        Even = "\"Good. That's settled, then.\"",
                        Grates = "\"You sound like you're reading it off a card.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"That's all? No speech?\"",
                        Lands = "\"The speech is coming. It has a dance break.\"",
                        Even = "\"That was the speech. Short and loud.\"",
                        Grates = "\"Wow. I open up and you want a speech.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Does that mean we work together?\"",
                        Lands = "\"It means I'm not writing your name down. Ever. Start there.\"",
                        Even = "\"It means you hear things from me first.\"",
                        Grates = "\"Wow, straight to business. Okay.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.friend.2", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(catching you after the comp, still out of breath) \"Hey. When they read the results, I looked for you first. Didn't even think about it. That means something.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I looked for you too.\"",
                        Lands = "\"See? Same team. Nobody else gets to know that.\"",
                        Even = "\"Good. Keep looking.\"",
                        Grates = "\"Did you, though? You hesitated.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then say it plainly. Are we working together?\"",
                        Lands = "\"Plainly? Yes. You and me. Don't make me say it twice.\"",
                        Even = "\"Yes. Quietly. Don't announce it.\"",
                        Grates = "\"Whoa. I was having a moment.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's not tell the house.\"",
                        Lands = "\"Smart. Our secret. I can keep one, I promise.\"",
                        Even = "\"Fine. Quiet friends. My least favorite kind.\"",
                        Grates = "\"Fine. Hide me like a bad habit.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.friend.3", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(sitting on the kitchen floor with you, sharing a spoon and a jar) \"I trust you. Out loud. On a show where nobody trusts anybody. So don't make me look silly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I won't. I trust you too.\"",
                        Lands = "\"Okay. We're doing this. Me and you.\"",
                        Even = "\"Good. The spoon's yours.\"",
                        Grates = "\"That came out a little fast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You already look a little silly on the floor.\"",
                        Lands = "\"Ha! Rude. The trust is still on.\"",
                        Even = "\"Fair. Doesn't change the trust part.\"",
                        Grates = "\"Wow. Trust is now under review.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No pressure, then.\"",
                        Lands = "\"None. Just a whole game on your shoulders. Kidding. Mostly.\"",
                        Even = "\"A little pressure. You'll be fine.\"",
                        Grates = "\"You could have just said the same back.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.close.1", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(grabbing your wrist in the hallway, voice low for once) \"I need you to do something for me. If my name comes up when it counts, you fight for me. Okay?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. Whatever they say.\"",
                        Lands = "\"That's it. That's all I needed. Okay, I'm going to go be normal now.\"",
                        Even = "\"Okay. Good. I'm holding you to that.\"",
                        Grates = "\"Okay. You said it fast, but I'm taking it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. I'll be honest if I'm asked.\"",
                        Lands = "\"Honest. Ugh. That's why I asked you and not them.\"",
                        Even = "(a little too brightly) \"Okay. Okay, sure. That's fair. That's totally fair.\"",
                        Grates = "\"Wow. Okay. Noted. Very noted.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.close.2", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(sitting on the edge of your bunk, not joking for once) \"Big ask. When the house turns, and it will, I need one person who stands up for me. Can that be you?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Favour = TalkFavour.Keep, Line = "\"I've got you. You won't be standing alone.\"",
                        Lands = "\"Okay. Wow. I'm going to pretend I didn't almost cry.\"",
                        Even = "\"Good. That's the answer I was hoping for.\"",
                        Grates = "\"Okay. Just mean it when it happens.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. Not if it costs me the game.\"",
                        Lands = "\"It's a fair answer. I'd probably say the same. Probably.\"",
                        Even = "(looking at the floor) \"Right. No, I get it. The game's the game.\"",
                        Grates = "\"Wow. Good to know where I stand.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.stage.close.3", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(in the storage room, the only place without a crowd) \"Real talk. If it's ever me or the house, I need you to pick me. Just once. When it matters.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. When it matters, it's you.\"",
                        Lands = "\"Then I owe you one, and I always pay up. Loudly.\"",
                        Even = "\"Okay. Then we're good. Really good.\"",
                        Grates = "\"Okay. I'll believe it when I see it, but okay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. I don't know what's coming.\"",
                        Lands = "\"Nobody does. At least you didn't lie to me.\"",
                        Even = "\"Nobody does. I just hoped you'd say yes anyway.\"",
                        Grates = "\"Right. Forget I asked.\"",
                    },
                },
            },
        };
    }
}
