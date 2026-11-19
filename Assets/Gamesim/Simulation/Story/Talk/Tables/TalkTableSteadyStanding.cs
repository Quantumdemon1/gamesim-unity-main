namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the steady pool's standing talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableSteadyStanding
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "steady.st.cereal", Pool = TalkPools.Steady, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(pouring the last of the cereal into two bowls) \"Half's yours. And since you're sitting there: you want to know how my week really looks?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Only what you're comfortable with.\"",
                        Lands = "\"Comfortable enough. Here it is.\"",
                        Even = "\"Fine. The short of it.\"",
                        Grates = "\"Don't handle me. Fine. Short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Yes. Straight.\"",
                        Lands = "\"Straight. Okay.\"",
                        Even = "\"Straight-ish. Eat while I talk.\"",
                        Grates = "\"Easy. I'm getting there.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Only if it's the truth this time.\"",
                        Lands = "\"Fair. It is.\"",
                        Even = "\"It's what I've got.\"",
                        Grates = "\"This time? Wow. You get the outline.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"Straight: {close} is who I trust. {worry} is who I watch. As for me: {safe}.\"",
                    Neutral = "\"I've got one person I trust and one I watch. Names stay with me. As for me: {safe}.\"",
                    Dishonest = "\"Straight: {close} is who I trust. {worry} is who I watch. As for me, I'm comfortable this week.\"",
                },
            },
            new TalkRow
            {
                Id = "steady.st.peel", Pool = TalkPools.Steady, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(on the yard bench, peeling an orange in one long strip) \"If this peel comes off in one piece, I'll tell you where I sit this week. Deal?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"It's going to break.\"",
                        Lands = "(it doesn't) \"Ha. One piece. A deal's a deal.\"",
                        Even = "\"Close enough. Deal's a deal.\"",
                        Grates = "\"Now it broke. Thanks. Short version, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Deal. And it stays between us.\"",
                        Lands = "\"I know it will. That's why.\"",
                        Even = "\"Good. Here.\"",
                        Grates = "\"It had better. Quickly, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Tell me or don't. Either way.\"",
                        Lands = "\"That's exactly why I will.\"",
                        Even = "\"All right.\"",
                        Grates = "\"Then you get the short one.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"So: I'm tight with {close}. {worry} worries me. And me? {safe}.\"",
                    Neutral = "\"So: I'm tight with somebody. Somebody worries me. And me? {safe}. Names another day.\"",
                    Dishonest = "\"So: I'm tight with {close}. {worry} worries me. And me? I'm fine.\"",
                },
            },
            new TalkRow
            {
                Id = "steady.st.storeroom", Pool = TalkPools.Steady, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(in the storeroom, counting cans that don't need counting) \"Nobody comes in here. If you want the real picture of my week, now's the time.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Quietly, then. It stays in this room.\"",
                        Lands = "\"Good. Here.\"",
                        Even = "\"Quietly. Here.\"",
                        Grates = "\"Don't tell me how to whisper. Fast, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Names. Quick, before someone walks in.\"",
                        Lands = "\"Ha. Quick, then.\"",
                        Even = "\"Fine. Fast.\"",
                        Grates = "\"Don't push. You get the short version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll take whatever you want to give.\"",
                        Lands = "\"That's the answer that gets all of it.\"",
                        Even = "\"Here's what I've got.\"",
                        Grates = "\"Don't be so careful with me. Here. Briefly.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"Real picture: {close} has my back. {worry} is the problem. Me? {safe}. Now help me count.\"",
                    Neutral = "\"Real picture: I've got a person. There's a problem. Me? {safe}. The rest stays in the can.\"",
                    Dishonest = "\"Real picture: {close} has my back. {worry} is the problem. Me? I'm not worried. Now help me count.\"",
                },
            },
            new TalkRow
            {
                Id = "steady.st.lamps", Pool = TalkPools.Steady, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(last two up, switching off lamps one at a time) \"Last lamp. Before it goes off: you want to know how this week looks from where I'm sitting?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"If you want to say it.\"",
                        Lands = "\"I do. Strange, but I do.\"",
                        Even = "\"All right.\"",
                        Grates = "\"Then maybe I don't. Fine. The bare bones.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Go on. I'll keep it in the dark.\"",
                        Lands = "\"I know. Here it is.\"",
                        Even = "\"Good. Here.\"",
                        Grates = "\"Don't make it sound like a deal. Headlines only.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Say it fast. The dark makes it easier.\"",
                        Lands = "\"Ha. It does. Okay.\"",
                        Even = "\"Fast, then.\"",
                        Grates = "\"It's not a joke. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"From here? {close} and me are solid. {worry} I don't trust. And me? {safe}. Okay. Lamp.\"",
                    Neutral = "\"From here? I've got one solid person and one I don't trust. And me? {safe}. Okay. Lamp.\"",
                    Dishonest = "\"From here? {close} and me are solid. {worry} I don't trust. And me? Sleeping fine. Okay. Lamp.\"",
                },
            },
        };
    }
}
