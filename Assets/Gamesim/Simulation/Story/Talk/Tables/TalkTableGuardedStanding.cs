namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the guarded pool's standing talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableGuardedStanding
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "guarded.st.storage", Pool = TalkPools.Guarded, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(in the storage room, one hand on the door so it can't swing open) \"You want to know where I am this week. You've been waiting for me to say it. Ask.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Where are you this week? No hedging.\"",
                        Lands = "\"No hedging. Once. Listen.\"",
                        Even = "\"Fine. The plain version.\"",
                        Grates = "\"You'll get what I give you. Not a word more.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only what you want to tell me.\"",
                        Lands = "\"That's why you get all of it.\"",
                        Even = "\"Then here's what I want to tell you.\"",
                        Grates = "\"Don't hand it back to me. I said ask.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Names first. I'll do the sums.\"",
                        Lands = "\"You would. All right. Do the math with this.\"",
                        Even = "\"You'll get names. Not the sums.\"",
                        Grates = "\"Names aren't sums. You get the short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"{close}. That's the one I'd trust. {worry} is who I watch. Me? {safe}. Now you know more than anyone.\"",
                    Neutral = "\"There's someone I trust. There's someone I watch. Me? {safe}. That's as far as it goes.\"",
                    Dishonest = "\"{close}. That's the one I'd trust. {worry} is who I'd watch, if I were you. Me? {safe}. Now you know.\"",
                },
            },
            new TalkRow
            {
                Id = "guarded.st.dishes", Pool = TalkPools.Guarded, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(drying the same plate for too long, eyes on the doorway) \"Ask me now. While the water's running. Nobody hears anything over a faucet.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Are you all right this week?\"",
                        Lands = "\"That's the right question first. Here's the rest.\"",
                        Even = "\"Mostly. Here's the rest.\"",
                        Grates = "\"Don't do the soft voice. Ask the real question.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Who's coming for you?\"",
                        Lands = "\"Straight to it. Good. Here.\"",
                        Even = "\"Nobody, I hope. Here's what I think.\"",
                        Grates = "\"Lower. And slower. You sound like a nomination.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Tell me everything and I'll cover you.\"",
                        Lands = "\"Big offer. I'll take it. Here's everything.\"",
                        Even = "\"I'll tell you some. Cover that.\"",
                        Grates = "\"You can't cover me. Don't promise it.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"I'm with {close}. Quietly. {worry} worries me more than I show. And me? {safe}.\"",
                    Neutral = "\"I'm with somebody. Quietly. Somebody worries me. And me? {safe}. Don't ask me who.\"",
                    Dishonest = "\"I'm with {close}. Quietly. {worry} worries me, and should worry you. And me? {safe}.\"",
                },
            },
            new TalkRow
            {
                Id = "guarded.st.roof", Pool = TalkPools.Guarded, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(late, the yard lights dimmed, sitting with their back to the wall) \"Most people in here ask me to pick a side. You never have. So I'll tell you where I am.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then pick one now. With me.\"",
                        Lands = "\"I'm closer to it than you'd think. Here's where I am.\"",
                        Even = "\"Not tonight. But here's where I am.\"",
                        Grates = "\"There. That's what everyone else does.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Where you stand changes my week too.\"",
                        Lands = "\"I know. That's half of why I'm telling you.\"",
                        Even = "\"It does. So listen properly.\"",
                        Grates = "\"So this is for you, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You don't owe me anything.\"",
                        Lands = "\"I know. That's the only reason it's safe to say.\"",
                        Even = "\"I know. I'm saying it anyway.\"",
                        Grates = "\"I didn't say I owed you. Don't make it a debt.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"If it came to it, I'd go with {close}. {worry} is the problem. Me? {safe}. Keep that to yourself.\"",
                    Neutral = "\"If it came to it, I know who I'd go with. I know who the problem is. Me? {safe}. Keep that.\"",
                    Dishonest = "\"If it came to it, I'd go with {close}. {worry} is the problem for both of us. Me? {safe}. Keep that to yourself.\"",
                },
            },
            new TalkRow
            {
                Id = "guarded.st.stairs", Pool = TalkPools.Guarded, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(sitting on the stairs, one step above you, voice flat) \"Three things. I'll say each once. Don't make me repeat them.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Say them. I'll remember.\"",
                        Lands = "\"You will. That's why it's you.\"",
                        Even = "\"Good. One. Two. Three.\"",
                        Grates = "\"Don't promise. Just listen.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(just nod and listen)",
                        Lands = "\"Good. That's the right way to hear this.\"",
                        Even = "\"All right. Here.\"",
                        Grates = "\"You can say something. I'm not reading a will.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Say all three at once. I can take it.\"",
                        Lands = "\"I know you can. Slowly anyway. Listen.\"",
                        Even = "\"Once each. Slowly.\"",
                        Grates = "\"My pace. Not yours.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"One: {close}. Two: {worry}. Three: {safe}. That's my week.\"",
                    Neutral = "\"One: I've got someone. Two: there's someone I don't like. Three: {safe}. That's my week.\"",
                    Dishonest = "\"One: {close}. Two: {worry}, and keep an eye there yourself. Three: {safe}. That's my week.\"",
                },
            },
        };
    }
}
