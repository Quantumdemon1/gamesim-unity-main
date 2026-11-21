namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the steady pool's friendship stage talks (slice S2, talk rules 2; ruling R1: the talks are C# tables). Three
    /// rows per stage, Kind = TalkKind.Stage, Stage = Acquainted, Friendly, Friend or CloseFriend; the Close friend talk asks a
    /// favor and its two replies carry TalkReply.Favour (Keep, "I've got you"; Refuse, "Can't promise that"). American spelling.
    /// </summary>
    public static class TalkTableSteadyStages
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "steady.stage.acquainted.1", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(handing you a mug before you ask) \"Black, no sugar. I had you figured out by the second morning. Pay attention and people get easy.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. That's kind of you.\"",
                        Lands = "\"It's a mug of coffee. But you're welcome.\"",
                        Even = "\"No trouble. The pot was on.\"",
                        Grates = "\"Don't make a fuss. It's coffee.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What else have you figured out?\"",
                        Lands = "\"That you hum when you do the dishes. Badly. I like it.\"",
                        Even = "\"Enough. I'm not giving you the whole list.\"",
                        Grates = "\"Not much. You don't make it easy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You notice a lot.\"",
                        Lands = "\"Somebody has to. You do too. I've seen you.\"",
                        Even = "\"Habit. It keeps a house running.\"",
                        Grates = "\"Just the coffee. Don't read into it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.acquainted.2", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(passing you a dish to dry without a word) \"You're on drying. I'm on washing. We've done this enough that I don't have to ask anymore.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's nice having a routine with someone.\"",
                        Lands = "\"It is. Keeps a person sane in here.\"",
                        Even = "\"Sure is. Grab the next one.\"",
                        Grates = "\"It's dishes. Don't make it a moment.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"One day I'll wash and you'll dry.\"",
                        Lands = "\"Not a chance. You leave spots. But I'll let you try.\"",
                        Even = "\"We'll see. Probably not.\"",
                        Grates = "\"You'd flood the counter. No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Works for me.\"",
                        Lands = "\"Good. Easy is good in here.\"",
                        Even = "\"Me too.\"",
                        Grates = "\"You could sound a little happier about it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.acquainted.3", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Acquainted,
                Open = "(saving you a seat at the long table with a folded towel) \"That's yours. People sit where they sit now, and you sit by me. That's just how it is.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad it's by you.\"",
                        Lands = "\"Well, good. The other end of the table talks too much.\"",
                        Even = "\"Sit down, then. It's getting cold.\"",
                        Grates = "\"Just sit. It's a chair.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"And if somebody else wants it?\"",
                        Lands = "\"Then they can ask me, and I'll say no. Politely.\"",
                        Even = "\"They can find their own towel.\"",
                        Grates = "\"Then go sit with them. I'm not fighting over a chair.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"People will notice where I sit.\"",
                        Lands = "\"Let them. It's dinner, not an alliance.\"",
                        Even = "\"They notice everything. Eat anyway.\"",
                        Grates = "\"If you're going to count the angles of dinner, sit somewhere else.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.friendly.1", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(wiping down the counter for the second time) \"Between us, the kitchen tells you everything. Who eats with who, who skips meals before a vote. Watch it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's it telling you this week?\"",
                        Lands = "\"That folks are nervous. Lots of skipped breakfasts. Keep eating yours.\"",
                        Even = "\"Not much yet. Give it a day.\"",
                        Grates = "\"That's for me to know. You just watch.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thanks for telling me that.\"",
                        Lands = "\"I don't tell everybody. Just people who clean up after themselves.\"",
                        Even = "\"Sure. Common sense, really.\"",
                        Grates = "\"Don't thank me. Just use it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So you're the house detective.\"",
                        Lands = "\"Somebody has to be, and I've got the apron.\"",
                        Even = "\"Something like that.\"",
                        Grates = "\"Laugh. You'll see I'm right.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.friendly.2", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(folding laundry on the couch, a pile for you too) \"I did yours too. Don't make it a thing. I'd rather keep busy than sit and listen to the whispering.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That's really kind. Thank you.\"",
                        Lands = "\"You'd do the same. I've seen you take out the trash nobody else will.\"",
                        Even = "\"It was one more load.\"",
                        Grates = "\"I said don't make it a thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's the whispering about?\"",
                        Lands = "\"Same as always. Who's next. I don't join in. You shouldn't either.\"",
                        Even = "\"Nothing worth hearing.\"",
                        Grates = "\"I just said I'd rather not listen.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll get the next load.\"",
                        Lands = "\"Deal. That's how a house works.\"",
                        Even = "\"Fine by me.\"",
                        Grates = "\"That's what you said last time.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.friendly.3", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                Open = "(on the back step with two cups of tea) \"You want to know something? This house is louder than mine ever was. You're one of the quiet spots in it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're one of mine too.\"",
                        Lands = "\"Well, good. We'll keep each other sane.\"",
                        Even = "\"Good. Stay quiet with me a minute.\"",
                        Grates = "\"Don't get sappy on me. Just sit.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'm not that quiet.\"",
                        Lands = "\"Quieter than the rest. That's a compliment in here.\"",
                        Even = "\"Compared to them, you're a library.\"",
                        Grates = "\"Well, you're getting louder by the minute.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Quiet spots don't last long in here.\"",
                        Lands = "\"Then we'll enjoy this one while it does.\"",
                        Even = "\"True. Sit anyway.\"",
                        Grates = "\"You don't have to ruin it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.friend.1", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(drying their hands, then just saying it) \"I'll say it plain, since that's how I am. I'm glad you're in here. Things are better with you around.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad you're here too.\"",
                        Lands = "\"Good. Then we look out for each other. Simple.\"",
                        Even = "\"Well. There it is, then.\"",
                        Grates = "\"You don't have to say it back just because I did.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Does that mean I can count on you?\"",
                        Lands = "\"In here, at the table, in the votes. Yes.\"",
                        Even = "\"Most days. That's more than most get.\"",
                        Grates = "\"Ask me that when you mean it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"That means a lot.\"",
                        Lands = "\"It should. I don't say it often.\"",
                        Even = "\"Good. Back to the dishes.\"",
                        Grates = "\"That's all? Fine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.friend.2", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(catching you on the stairs after the vote) \"I'll tell you where my vote went, every week. I don't do that for anybody else. You've earned it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'll do the same for you.\"",
                        Lands = "\"Good. That's how trust works in a house like this.\"",
                        Even = "\"Fair trade.\"",
                        Grates = "\"Don't promise it if you won't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you for trusting me.\"",
                        Lands = "\"You've been straight with me. That's all it takes.\"",
                        Even = "\"Don't make me regret it.\"",
                        Grates = "\"Don't thank me. Just don't waste it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Somebody might notice we compare votes.\"",
                        Lands = "\"Then we'll be careful. But I'm still telling you.\"",
                        Even = "\"Maybe. I'll still tell you.\"",
                        Grates = "\"If you're that worried, I can stop.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.friend.3", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.Friend,
                Open = "(at the stove, stirring, not looking up) \"The folks back home would like you. That's about the best thing I can say about anybody.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That means a lot, coming from you.\"",
                        Lands = "\"It should. They're picky. So am I.\"",
                        Even = "\"Well, it's true.\"",
                        Grates = "\"Don't make a face. I meant it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Would they let me dry the dishes?\"",
                        Lands = "\"They'd make you wash. Then they'd feed you twice.\"",
                        Even = "\"Only if you didn't leave spots.\"",
                        Grates = "\"Not if you keep joking about it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then trust me with your vote this week.\"",
                        Lands = "\"You already have it. I just hadn't said so.\"",
                        Even = "\"Maybe. Let's see the week first.\"",
                        Grates = "\"Don't turn a nice thing into a deal.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.close.1", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(sitting down across from you at the empty table) \"I don't ask for much. But if my name goes up, I need you speaking for me. Can I count on that?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. You can count on it.\"",
                        Lands = "(squeezing your shoulder) \"Good. Then I'll sleep tonight. Thank you.\"",
                        Even = "\"Good. That's settled, then.\"",
                        Grates = "\"All right. I'll hold you to it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. I won't lie to you about it.\"",
                        Lands = "\"I appreciate the honesty. I'd rather that than a promise you can't keep.\"",
                        Even = "\"I appreciate the honesty. I do. It just stings a little.\"",
                        Grates = "\"Well. I know where I stand, then.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.close.2", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(in the laundry room, keeping their voice down) \"When it gets close to the end, people forget who fed them. Don't forget me. Have my back.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you. I won't forget.\"",
                        Lands = "\"That's all I wanted to hear. I'll have yours, too.\"",
                        Even = "\"Good. Then I won't worry about it.\"",
                        Grates = "\"All right. We'll see when we get there.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. The end changes people.\"",
                        Lands = "\"It does. I'm glad you said so instead of pretending.\"",
                        Even = "(folding a towel a little too hard) \"It does. I just hoped it wouldn't change you.\"",
                        Grates = "\"Then I'd better not count on you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.stage.close.3", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = FriendStage.CloseFriend,
                Open = "(on the back step, after everyone's gone in) \"Favor to ask. If they come for me, be the one who says no. Out loud, at the table.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Favour = TalkFavour.Keep, Line = "\"I've got you. I'll say it out loud.\"",
                        Lands = "\"Then I'm not worried. Not much, anyway.\"",
                        Even = "\"Good. That's what I needed.\"",
                        Grates = "\"All right. Just don't say it too loud.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that. I can't be loud right now.\"",
                        Lands = "\"That's fair. You've got your own neck to think about.\"",
                        Even = "\"All right. I understand. I'll manage.\"",
                        Grates = "\"I see. I'll find someone who can.\"",
                    },
                },
            },
        };
    }
}
