namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the All-Stars' game-register pool's friendship stage talks (slice S2, talk rules 2; ruling R1: the talks are C# tables). Three
    /// rows per stage, Kind = TalkKind.Stage, Stage = Acquainted, Friendly, Friend or CloseFriend; the Close friend talk asks a
    /// favor and its two replies carry TalkReply.Favour (Keep, "I've got you"; Refuse, "Can't promise that"). American spelling.
    /// Game register only (rulings R7, R14, R15): a friendship in the game, nothing romantic, nothing about anyone's life outside it.
    /// </summary>
    public static class TalkTableStarStages
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "star.stage.acquainted.1", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(passing you in the hallway, then stopping) \"You're the one who wipes the counter after every comp. I've clocked you. From me, that's a compliment.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I clean when I'm thinking.\"",
                        Lands = "\"I know. Everyone else thinks out loud. You think with a sponge. Smart.\"",
                        Even = "\"Fair. Keep thinking.\"",
                        Grates = "\"Thinking that hard? Somebody's going to notice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You keep track of the cleaning?\"",
                        Lands = "\"I keep track of everything. The cleaning is the easy part.\"",
                        Even = "\"I keep track of everything. It's the job.\"",
                        Grates = "\"Laugh. It tells me more than the speeches do.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Good to know you're watching.\"",
                        Lands = "\"Everyone's watching. I'm just honest about it. Respect.\"",
                        Even = "\"Always. Get used to it.\"",
                        Grates = "\"Don't get nervous. It's not that kind of watching.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.acquainted.2", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(sitting down beside you at the yard table) \"I've played enough of these to know a name from a player. You've stopped being a name to me. Don't let it go to your head.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Good. You stopped being a name to me too.\"",
                        Lands = "\"Then we're even. That's a good place to start in here.\"",
                        Even = "\"Mm. We'll see if it sticks.\"",
                        Grates = "\"Easy. Everyone says that early on.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"So what am I, then?\"",
                        Lands = "\"Someone I talk to before the vote. That's a short list.\"",
                        Even = "\"Ask me again after the next eviction.\"",
                        Grates = "\"Pushing already? Slow down.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll take it.\"",
                        Lands = "\"Good answer. No speech. I respect that.\"",
                        Even = "\"Take it. It's free this week.\"",
                        Grates = "\"You're supposed to say something back.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.acquainted.3", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(nodding at you across the room after the comp) \"You know what I check first now? Where you're standing. You've become one of the fixed points in this house.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Is that good or bad for me?\"",
                        Lands = "\"Good. Fixed points are who I build around.\"",
                        Even = "\"Depends on the week. Today, good.\"",
                        Grates = "\"Ask questions like that and it gets worse.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Fixed points make easy targets.\"",
                        Lands = "\"Now you're thinking like a veteran. Right. So we keep it quiet.\"",
                        Even = "\"Sometimes. Not if they're smart.\"",
                        Grates = "\"Then move. See if I notice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll try to stand somewhere more interesting.\"",
                        Lands = "\"Don't. Predictable is how you last.\"",
                        Even = "\"You'd stick out anyway.\"",
                        Grates = "\"Keep joking. That's a tell, too.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.friendly.1", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(leaning on the counter, voice down) \"House read, free of charge. The loud side of the room is pretending to be one alliance. Watch it crack.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"When do you think it cracks?\"",
                        Lands = "\"Soon. The first veto that matters. Be standing somewhere else.\"",
                        Even = "\"When it costs them something. Not before.\"",
                        Grates = "\"You'll know when I know. Maybe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Why tell me?\"",
                        Lands = "\"Because you'd have seen it anyway. I'd rather we see it together.\"",
                        Even = "\"You were standing here.\"",
                        Grates = "\"Don't overthink a gift.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thanks. I owe you one.\"",
                        Lands = "\"Careful, I collect. But you're easy to work with, so it's cheap.\"",
                        Even = "\"You do. I'll remember.\"",
                        Grates = "\"Don't owe me. Just use it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.friendly.2", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(in the yard after the comp, both of you still muddy) \"Here's what nobody tells you about comps. Losing well wins you more votes than winning loud. You lost well.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Coming from you, that means a lot.\"",
                        Lands = "\"It should. I don't hand that out. Go get cleaned up.\"",
                        Even = "\"It's just a read. Take it.\"",
                        Grates = "\"Don't get sentimental on me. It was a comp.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll try to lose well again next time.\"",
                        Lands = "\"Don't you dare. Win next time. Just win quietly.\"",
                        Even = "\"Let's not make it a habit.\"",
                        Grates = "\"Joke if you want. The house was watching.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You lost on purpose, didn't you?\"",
                        Lands = "(grinning) \"Maybe. You noticed. That's why we're talking.\"",
                        Even = "\"I'll never tell.\"",
                        Grates = "\"Accusations already? Bold.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.friendly.3", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(handing you a water bottle in the quiet hour) \"I don't have to perform with you. In here that's worth more than a veto. Relax, I'm not making a deal.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Then I won't either.\"",
                        Lands = "\"Good. One honest corner of the house. Rare.\"",
                        Even = "\"A deal that isn't a deal, then.\"",
                        Grates = "\"You're already performing. I can tell.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Not making a deal yet?\"",
                        Lands = "\"Yet. Fine. You're quick, I'll give you that.\"",
                        Even = "\"Not today.\"",
                        Grates = "\"And there it is. Business.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's nice to just talk.\"",
                        Lands = "\"It is. Most of this game is noise. This isn't.\"",
                        Even = "\"Mm. Don't get used to it.\"",
                        Grates = "\"Careful. Nice is how people get blindsided.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.friend.1", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(crossing the living room to you) \"I'm going to say it out loud, which I never do in a game. I'm glad you're in here. You make this game better.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That means a lot from someone who's done this before.\"",
                        Lands = "\"It's not charity. You've earned it. Keep playing like this.\"",
                        Even = "\"Don't frame it. Just keep going.\"",
                        Grates = "\"Don't make it about me. It's about how you play.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Glad as in we're working together?\"",
                        Lands = "\"As in you hear my plans before the house does. That's working together.\"",
                        Even = "\"Glad as in I won't write your name lightly.\"",
                        Grates = "\"Ask me plainer when it's quieter.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Say it louder. The cameras missed it.\"",
                        Lands = "(laughing) \"Not a chance. Once is my limit.\"",
                        Even = "\"They didn't. That's the problem.\"",
                        Grates = "\"And this is why I never say it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.friend.2", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(by the memory wall, voice low) \"I trust your reads now. I'd take your read on a vote over most of the house. I don't say that lightly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Then let's compare reads every week.\"",
                        Lands = "\"Every week. Before the vote, not after. Deal.\"",
                        Even = "\"Most weeks. Some reads I keep.\"",
                        Grates = "\"Let's not put trust on a schedule.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I trust yours too.\"",
                        Lands = "\"Good. Two good reads beat one great one.\"",
                        Even = "\"You should. Most of the time.\"",
                        Grates = "\"Easy. Trust isn't a reply, it's a record.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Lots of players have said that to you.\"",
                        Lands = "\"And I remember who kept their word. You will.\"",
                        Even = "\"They have. Few meant it.\"",
                        Grates = "\"And now you sound like them.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.friend.3", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(sitting with you on the stairs after the eviction) \"Plenty of people in here want to beat me. You're one of the few I wouldn't mind losing to.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd be glad to sit next to you at the end.\"",
                        Lands = "\"Then let's get there. As long as it lasts.\"",
                        Even = "\"Get there first. Then we'll talk.\"",
                        Grates = "\"Don't plan the finale yet. It's bad luck.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then don't put me up.\"",
                        Lands = "\"I wasn't going to. Now I'm saying it.\"",
                        Even = "\"Not this week. That's a promise.\"",
                        Grates = "\"Asking out loud makes it harder.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll make sure you get the chance to lose to me.\"",
                        Lands = "\"Ha. Good. A jury respects a rivalry.\"",
                        Even = "\"We'll see who's sitting where.\"",
                        Grates = "\"Careful. That sounds like a threat.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.close.1", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(catching you alone by the storage room) \"I need a favor, game to game. If my name comes up in the HoH room, tell me first. Before anyone.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. You'll hear it from me first.\"",
                        Lands = "\"Then I owe you one. I don't forget who I owe.\"",
                        Even = "\"Good. That's all I needed.\"",
                        Grates = "\"Okay. Promises are easy in here. We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. Sometimes I won't know in time.\"",
                        Lands = "(a slow nod) \"Fair. That's an honest answer. I'll take honest.\"",
                        Even = "\"Fair. Tell me when you can, then.\"",
                        Grates = "\"Then I'll find out the hard way, like everyone else.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.close.2", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(at the far end of the yard, voice low) \"Here's my ask. When the votes get counted for real, I need one vote I never have to wonder about. Yours.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Favour = TalkFavour.Keep, Line = "\"I've got you. You won't have to wonder.\"",
                        Lands = "\"Good. That's the most useful thing anyone's said to me in here.\"",
                        Even = "\"Good. Then I can stop counting you.\"",
                        Grates = "\"Okay. A vote is only real on the night.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. My vote has to stay mine.\"",
                        Lands = "\"Respect. It should. I had to ask.\"",
                        Even = "\"Fair. I'd say the same.\"",
                        Grates = "\"Then I'll keep counting you like everyone else.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.stage.close.3", Pool = TalkPools.Star, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(on the backyard bench, both of you facing the house) \"A veteran's favor. If they come at me, don't defend me out loud. Just slow it down and tell me. I'll do the rest.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. I'll slow it down and find you.\"",
                        Lands = "\"That's exactly it. You get the game. That's why I asked you.\"",
                        Even = "\"Good. That's all it takes.\"",
                        Grates = "\"Okay. Don't make it obvious when you do.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. I can't be seen slowing things down.\"",
                        Lands = "\"Smart. Annoying, but smart. I'd have said the same.\"",
                        Even = "\"Fair. You've got your own game to play.\"",
                        Grates = "\"Then I asked the wrong player.\"",
                    },
                },
            },
        };
    }
}
