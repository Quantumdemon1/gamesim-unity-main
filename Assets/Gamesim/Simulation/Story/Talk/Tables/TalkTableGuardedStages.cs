namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the guarded pool's friendship stage talks (slice S2, talk rules 2; ruling R1: the talks are C# tables). Three
    /// rows per stage, Kind = TalkKind.Stage, Stage = Acquainted, Friendly, Friend or CloseFriend; the Close friend talk asks a
    /// favor and its two replies carry TalkReply.Favour (Keep, "I've got you"; Refuse, "Can't promise that"). American spelling.
    /// </summary>
    public static class TalkTableGuardedStages
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "guarded.stage.acquainted.1", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(nodding once as you walk in) \"You knock before you come into the bedroom. Nobody else does. I noticed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Seemed like the polite thing to do.\"",
                        Lands = "\"It is. Keep doing it.\"",
                        Even = "\"Mm. Most people skip polite.\"",
                        Grates = "\"Don't fish for credit.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"You notice a lot of small things.\"",
                        Lands = "\"Small things are honest. Big things are staged.\"",
                        Even = "\"It's what I do.\"",
                        Grates = "\"And you noticed me noticing. Fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Should I knock on the yard door too?\"",
                        Lands = "(almost a smile) \"Funny. Once.\"",
                        Even = "\"No. Just the bedroom.\"",
                        Grates = "\"Never mind. Forget I said it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.acquainted.2", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(sliding over on the couch, just enough to make room) \"You can sit. You don't talk when there's nothing to say. That's rare in here.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks.\"",
                        Lands = "\"Mm. You're learning.\"",
                        Even = "\"Mm. Stay as long as you want.\"",
                        Grates = "\"That was already a speech. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'd rather say nothing than say something fake.\"",
                        Lands = "\"Same. That's why you can sit.\"",
                        Even = "\"Good rule. Few keep it.\"",
                        Grates = "\"That sounded rehearsed.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I can talk if you want. A lot.\"",
                        Lands = "(a short laugh) \"Please don't.\"",
                        Even = "\"I'll risk the quiet.\"",
                        Grates = "\"Then sit somewhere else.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.acquainted.3", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(handing back a sock you dropped, folded) \"Yours. I know your things now. That's not a compliment. It's just true.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Sounds a little like a compliment.\"",
                        Lands = "(looking away) \"Maybe a small one.\"",
                        Even = "\"It isn't. Mostly.\"",
                        Grates = "\"It isn't. Don't push it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thanks for picking it up.\"",
                        Lands = "\"You'd have done it for me. That's why.\"",
                        Even = "\"It was on the floor.\"",
                        Grates = "\"It was in my way.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"What else do you know about me?\"",
                        Lands = "\"Enough. More than you think. Less than I'd like.\"",
                        Even = "\"Not much. Yet.\"",
                        Grates = "\"That's a lot of question for a sock.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.friendly.1", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(low, by the sink, not turning around) \"Small thing. The ones who laugh loudest in this house sleep worst. I hear them. Thought you should know.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why tell me?\"",
                        Lands = "\"Because you'll use it quietly. Not like the rest.\"",
                        Even = "\"You were here. That's all.\"",
                        Grates = "\"Forget it, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"That's useful. I'll keep it to myself.\"",
                        Lands = "\"Good. That's why I said it to you.\"",
                        Even = "\"That's the idea.\"",
                        Grates = "\"You'd better.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You don't sleep much either, do you?\"",
                        Lands = "(a pause) \"No. You noticed. Fine.\"",
                        Even = "\"Enough. Don't worry about it.\"",
                        Grates = "\"This isn't about me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.friendly.2", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(sharing the shade of the one umbrella in the yard) \"I don't mind you being here. I mind most people. Take that how you want.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll take it as a good thing.\"",
                        Lands = "\"It is. Don't tell anyone.\"",
                        Even = "\"It's meant as one.\"",
                        Grates = "\"Don't make me regret saying it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"High praise. I'll put it on a shirt.\"",
                        Lands = "(nearly laughing) \"Don't you dare.\"",
                        Even = "\"Please don't.\"",
                        Grates = "\"And now I mind a little.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I don't mind you either.\"",
                        Lands = "\"Good. Then we sit.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"You didn't have to say anything.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.friendly.3", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(at the edge of the pool, feet nowhere near the water) \"I've told you more this week than I tell most people in a month. I'm not sure how that happened.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad you did.\"",
                        Lands = "\"So am I. Strangely.\"",
                        Even = "\"We'll see if I am.\"",
                        Grates = "\"Don't make it sound like a confession.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"I won't repeat any of it.\"",
                        Lands = "\"I know. That's how it happened.\"",
                        Even = "\"Good.\"",
                        Grates = "\"You shouldn't have to say that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Maybe you wanted someone to tell.\"",
                        Lands = "(after a long breath) \"Maybe. Don't get smug about it.\"",
                        Even = "\"Maybe. Don't read into it.\"",
                        Grates = "\"Or maybe I'll stop now.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.friend.1", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(finding you in the storage room on purpose) \"I don't say this. So listen. I'm glad you're in here. That's it. Don't make me repeat it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad you're here too.\"",
                        Lands = "\"Good. Now we both said it. Done.\"",
                        Even = "\"Fine. Noted.\"",
                        Grates = "\"You said that too fast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I heard you. Once is enough.\"",
                        Lands = "(a real smile, briefly) \"That's exactly right.\"",
                        Even = "\"Good.\"",
                        Grates = "\"You could have said something back.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Does that change how you vote?\"",
                        Lands = "\"It means I won't write your name. Not this week, not soon.\"",
                        Even = "\"It means I'll think twice. That's a lot for me.\"",
                        Grates = "\"I tell you that and you ask about votes.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.friend.2", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(walking the edge of the yard with you, keeping pace) \"I trust about one person in here at a time. Right now it's you. I'm telling you so you know what it costs.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What does it cost?\"",
                        Lands = "\"My one guess. If I'm wrong about you, I'm done.\"",
                        Even = "\"Enough. Don't waste it.\"",
                        Grates = "\"If you have to ask, maybe too much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll be careful with it.\"",
                        Lands = "\"I know you will. That's why it's you.\"",
                        Even = "\"You'd better be.\"",
                        Grates = "\"Don't promise. Just do it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"One person at a time is a careful game.\"",
                        Lands = "\"It's the only game I play. You're in it now.\"",
                        Even = "\"It's kept me safe so far.\"",
                        Grates = "\"Don't analyze me. I'm trying to be nice.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.friend.3", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(sliding you half their plate without comment) \"Eat. I saw you skip lunch before the comp. Someone in here should be looking out for you. I am.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. I'll look out for you too.\"",
                        Lands = "\"I know. You already do. I've noticed.\"",
                        Even = "\"Good. Eat.\"",
                        Grates = "\"Just eat. Don't make a speech.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this how you show you care?\"",
                        Lands = "\"It's the only way I know how. Eat.\"",
                        Even = "\"It's food. Take it.\"",
                        Grates = "\"Never mind. I'll eat it myself.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Look out for me in the votes, too.\"",
                        Lands = "\"Already doing it. I just don't say so.\"",
                        Even = "\"That's the plan. Quietly.\"",
                        Grates = "\"Food first. Deals later.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.close.1", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(at the bottom of the stairs, blocking your way, very quiet) \"I need one thing. When my name comes up, don't let them decide without you. Will you?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. They won't decide without me.\"",
                        Lands = "(letting out a breath) \"Okay. That's more than I thought I'd get.\"",
                        Even = "\"Good. Remember you said it.\"",
                        Grates = "\"Words are cheap in here. We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. I won't lie to you.\"",
                        Lands = "(a long look) \"No. You wouldn't. That's why I asked you.\"",
                        Even = "(a long look) \"No. You wouldn't. I asked anyway.\"",
                        Grates = "\"Fine. I shouldn't have asked.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.close.2", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(outside, where the cameras can only see your backs) \"I've never asked anyone in a game for anything. I'm asking you. When it counts, have my back.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. When it counts.\"",
                        Lands = "\"Good. I'll remember this. I remember everything.\"",
                        Even = "\"Okay. That's enough.\"",
                        Grates = "\"Hm. Say it like you mean it next time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. Not yet.\"",
                        Lands = "\"Not yet. Fine. I'll remember you said it straight.\"",
                        Even = "\"Not yet. All right.\"",
                        Grates = "\"Then I'll stop asking.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.stage.close.3", Pool = TalkPools.Guarded, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(folding a shirt, not meeting your eyes) \"If someone's pulling votes against me, tell me first. Before anyone. That's the favor.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Favour = TalkFavour.Keep, Line = "\"I've got you. You'll hear it first.\"",
                        Lands = "(nodding once) \"Then we're square. More than square.\"",
                        Even = "\"Good. Before anyone.\"",
                        Grates = "\"Don't say it unless you'll do it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. I can't always see it coming.\"",
                        Lands = "\"Fair. Nobody can. Just try.\"",
                        Even = "\"Then just try. That's all. Try.\"",
                        Grates = "\"Then what good are you to me?\"",
                    },
                },
            },
        };
    }
}
