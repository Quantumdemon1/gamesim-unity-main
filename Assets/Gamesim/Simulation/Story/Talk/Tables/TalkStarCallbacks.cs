namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk (slice S3, talk rules 3): each All-Star template's running-joke callback, the talk spec 3.10 gives a player
    /// who laughed off a turn-aside (flag running-joke, priority 80). The row form has no kind for it yet, so these rows are
    /// held here, outside <see cref="TalkTables"/>, and nothing reads them: <c>Kind</c> is a placeholder, <c>Owner</c> the
    /// template. One row per All-Stars template, all twelve alike; game register only. American spelling.
    /// </summary>
    public static class TalkStarCallbacks
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "dan.rom.joke.1", Owner = "dan-gheesling", Kind = TalkKind.Rhythm,
                Open = "(setting down the puzzle) \"Still trying to win me over? Wrong game. But I've got a move for you this week.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What's the move?\"",
                        Lands = "\"You'll think it was your idea. That's how you know it's a good one.\"",
                        Even = "\"Patience. You'll see it Thursday.\"",
                        Grates = "\"Never mind. You're not ready.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm listening.\"",
                        Lands = "\"Good. People who listen last in here.\"",
                        Even = "\"Then listen close.\"",
                        Grates = "\"Are you, though?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Just tell me plainly.\"",
                        Lands = "\"Plainly? Keep your head down till the vote.\"",
                        Even = "\"Fine. Watch the vote.\"",
                        Grates = "\"Plain doesn't win this game.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "will.rom.joke.1", Owner = "dr-will-kirby", Kind = TalkKind.Rhythm,
                Open = "(as you pass the couch) \"Still trying? Good. Because I've got a vote read for you, and it costs one secret.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"One secret. A small one.\"",
                        Lands = "\"Small secrets are the best kind. Lean in.\"",
                        Even = "\"Fine. Small is fine.\"",
                        Grates = "\"Too small. Try again.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Go on, then. What have you got?\"",
                        Lands = "\"That's the spirit. Lean in.\"",
                        Even = "\"Patience. Later.\"",
                        Grates = "\"Not with that attitude.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I don't trade secrets.\"",
                        Lands = "\"Admirable. Useless, but admirable.\"",
                        Even = "\"Then we're at an impasse.\"",
                        Grates = "\"Then you'll never know who's coming for you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.joke.1", Owner = "derrick-levasseur", Kind = TalkKind.Rhythm,
                Open = "(by the kitchen counter) \"You took that well. Most people don't. I noticed. I notice everything.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Even that?\"",
                        Lands = "\"Especially that. How people take a no tells me plenty.\"",
                        Even = "\"Even that.\"",
                        Grates = "\"You'd be surprised.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Glad we're good.\"",
                        Lands = "\"We are. Better than good. I trust you more now.\"",
                        Even = "\"We are.\"",
                        Grates = "\"Don't overthink it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What else did you notice?\"",
                        Lands = "\"That you keep calm. That's rare. That's useful.\"",
                        Even = "\"Enough.\"",
                        Grates = "\"That's mine to know.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.joke.1", Owner = "janelle-pierzina", Kind = TalkKind.Rhythm,
                Open = "(tossing you a water) \"Still trying to win me over? Win a comp. That's how you get my attention.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Deal. Watch me.\"",
                        Lands = "\"Ha! I'll be watching. Don't embarrass us.\"",
                        Even = "\"Okay, prove it.\"",
                        Grates = "\"Big talk.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll train with you.\"",
                        Lands = "\"Yes! Finally, someone who gets it.\"",
                        Even = "\"Okay. Six in the morning.\"",
                        Grates = "\"We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll beat you.\"",
                        Lands = "\"Oh, now we're talking. Bring it.\"",
                        Even = "\"Sure you will.\"",
                        Grates = "\"Nobody beats me. Sorry.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.joke.1", Owner = "cody-calafiore", Kind = TalkKind.Rhythm,
                Open = "(spotting you in the yard) \"You still owe me a laugh for that one. Come throw the ball. You're on my team now.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only if you go easy.\"",
                        Lands = "\"Always. That's what teammates do.\"",
                        Even = "\"Sure. Come on.\"",
                        Grates = "\"Lift your own weights.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm in.\"",
                        Lands = "\"Good. Loyal people stick together.\"",
                        Even = "\"Cool.\"",
                        Grates = "\"We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What do you need?\"",
                        Lands = "\"Someone I can trust at the vote. You.\"",
                        Even = "\"Just a teammate.\"",
                        Grates = "\"Nothing yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.joke.1", Owner = "danielle-reyes", Kind = TalkKind.Rhythm,
                Open = "(by the diary room door) \"I'll give you this: you handled that well. I'm still deciding what it tells me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Let me know when you decide.\"",
                        Lands = "\"You'll know. Maybe not right away.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"You'll be the last to know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad we're okay.\"",
                        Lands = "\"We are. More than okay.\"",
                        Even = "\"We are.\"",
                        Grates = "\"For now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What does it tell you?\"",
                        Lands = "\"That you think before you act. Most don't.\"",
                        Even = "\"Enough.\"",
                        Grates = "\"That's mine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.joke.1", Owner = "vanessa-rousso", Kind = TalkKind.Rhythm,
                Open = "(by the kitchen table) \"You took that so well. I've updated my read. Want to know the new odds?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Are they in my favor?\"",
                        Lands = "\"Way up. Don't blow it.\"",
                        Even = "\"Slightly.\"",
                        Grates = "\"Not yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Tell me.\"",
                        Lands = "\"You're safe with me this week. That's the read.\"",
                        Even = "\"Decent.\"",
                        Grates = "\"Later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Do I want to know?\"",
                        Lands = "\"Yes. They're good. Trust me.\"",
                        Even = "\"Maybe not.\"",
                        Grates = "\"Probably not.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.joke.1", Owner = "tyler-crispen", Kind = TalkKind.Rhythm,
                Open = "(passing you in the yard) \"Still laughing about that, by the way. In a good way. You're cool.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'm very cool.\"",
                        Lands = "\"Super cool. The coolest. Okay, second coolest.\"",
                        Even = "\"Basically.\"",
                        Grates = "\"Okay, chill.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Glad we're good.\"",
                        Lands = "\"Always. You're one of my people in here.\"",
                        Even = "\"Yeah, totally.\"",
                        Grates = "\"We're fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Who's going up this week?\"",
                        Lands = "\"Ha. Straight to it. I like that. Come here.\"",
                        Even = "\"Not sure yet.\"",
                        Grates = "\"Can't tell you that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.joke.1", Owner = "chelsie-baham", Kind = TalkKind.Rhythm,
                Open = "(nudging you by the fridge) \"You know you're one of the only people who made me laugh this week?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only one of them?\"",
                        Lands = "\"Okay, the main one. Don't tell anyone.\"",
                        Even = "\"One of a few.\"",
                        Grates = "\"Don't push it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Happy to help.\"",
                        Lands = "\"You do help. More than you know.\"",
                        Even = "\"Thanks.\"",
                        Grates = "\"Okay, okay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Is that a good sign?\"",
                        Lands = "\"For you? Very good.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Too early to say.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.joke.1", Owner = "rachel-reilly", Kind = TalkKind.Rhythm,
                Open = "(slapping the counter) \"You know what? You took that like a champ. I respect that. A lot.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I am a champ.\"",
                        Lands = "\"Ha! Prove it at the comp!\"",
                        Even = "\"Okay, sure.\"",
                        Grates = "\"Not yet you're not.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Glad we're good.\"",
                        Lands = "\"Better than good! Teammates!\"",
                        Even = "\"Totally.\"",
                        Grates = "\"We're fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Who's your target?\"",
                        Lands = "\"Oh, I like you. Come here.\"",
                        Even = "\"Not telling. Yet.\"",
                        Grates = "\"Nice try.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.joke.1", Owner = "xavier-prather", Kind = TalkKind.Rhythm,
                Open = "(by the kitchen counter) \"You handled that well. I've thought about it. That's the kind of player I'd want beside me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Beside you? Interesting.\"",
                        Lands = "\"In the game. To the end, maybe.\"",
                        Even = "\"In the game.\"",
                        Grates = "\"Don't read too much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd want you beside me too.\"",
                        Lands = "\"Then let's make it count.\"",
                        Even = "\"Good.\"",
                        Grates = "\"We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What do you need?\"",
                        Lands = "\"Your vote, steady. That's all.\"",
                        Even = "\"Time.\"",
                        Grates = "\"Nothing today.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.joke.1", Owner = "jun-song", Kind = TalkKind.Rhythm,
                Open = "(tasting from the spoon) \"You know, you took that so well. People who take a no well last a long time in here.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is that a prediction?\"",
                        Lands = "\"It's a promise. If you keep quiet.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"No promises.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Glad we're good.\"",
                        Lands = "\"Very good. Here, taste this.\"",
                        Even = "\"We are.\"",
                        Grates = "\"Sure.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's fighting this week?\"",
                        Lands = "\"Everyone but us. That's the point.\"",
                        Even = "\"The usual ones.\"",
                        Grates = "\"Not telling.\"",
                    },
                },
            },
        };
    }
}
