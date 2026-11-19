namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the star pool's standing talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableStarStanding
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "star.st.board", Pool = TalkPools.Star, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(dragging two chairs to the far end of the yard) \"You want my read of the board? I'll give you where I sit on it. Once.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Yes. Where you sit, and who's next to you.\"",
                        Lands = "\"Straight question. You've earned a straight board.\"",
                        Even = "\"Fine. The short read.\"",
                        Grates = "\"You want the whole board for free. You get my corner.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only if you want to give it.\"",
                        Lands = "\"I do. That's what surprises me.\"",
                        Even = "\"I'll give you some of it.\"",
                        Grates = "\"Then I'll give you less.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Give me the numbers. I'll do the rest.\"",
                        Lands = "\"Ha. A fellow counter. All right.\"",
                        Even = "\"Numbers, then. Rough ones.\"",
                        Grates = "\"I'm not your spreadsheet. Words only.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"Closest to {close}. {worry} is the problem piece. Me? {safe}. That's the board.\"",
                    Neutral = "\"I've got two or three people. One piece worries me. Me? {safe}. That's all the board you get.\"",
                    Dishonest = "\"Closest to {close}. {worry} is the problem piece. Me? Comfortable. That's the board.\"",
                },
            },
            new TalkRow
            {
                Id = "star.st.caps", Pool = TalkPools.Star, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(lining up bottle caps on the yard table like game pieces) \"Here's the house as I see it. That cap's me. Watch where I put the others.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Which cap am I?\"",
                        Lands = "\"The one I keep in my hand. Good sign. Watch.\"",
                        Even = "\"Still in the box. Watch the others first.\"",
                        Grates = "\"The one that keeps talking. Watch, please.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Move them yourself. Show me who you'd cut.\"",
                        Lands = "\"Ha. Nobody gets cut on a table. Here's who stands where.\"",
                        Even = "\"I'll show you who's where. Not who's gone.\"",
                        Grates = "\"Cutting is a word I don't say out loud. You get less.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thanks for showing me this.\"",
                        Lands = "\"Don't thank me. It's a habit, and you're the habit this week.\"",
                        Even = "\"Look first. Thank me after.\"",
                        Grates = "\"Don't make it a moment. It's caps.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"This one's {close}. Right beside me. This one's {worry}. Far side. Me? {safe}.\"",
                    Neutral = "\"A few caps near me. One cap I don't like. Me? {safe}. The rest stays in my pocket.\"",
                    Dishonest = "\"This one's {close}. Right beside me. This one's {worry}. Far side. Me? Middle of the table.\"",
                },
            },
            new TalkRow
            {
                Id = "star.st.oneperson", Pool = TalkPools.Star, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(finding you alone by the pantry) \"Old habit. Once a week I tell one person exactly where I stand. This week it's you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad it's me.\"",
                        Lands = "\"So am I. Don't spend it all at once.\"",
                        Even = "\"Good. Listen, then.\"",
                        Grates = "\"Don't be glad. Be careful with it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Why me? What do you want back?\"",
                        Lands = "\"Fair. Your read back, next week. That's the price.\"",
                        Even = "\"Nothing yet. Remember it was free.\"",
                        Grates = "\"That question just cost you the long version.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You can change your mind about that.\"",
                        Lands = "\"I won't. You just showed me why.\"",
                        Even = "\"Too late. Here.\"",
                        Grates = "\"Maybe I will. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"I trust {close}. I don't trust {worry}. Me? {safe}. Now you know what I know.\"",
                    Neutral = "\"There's someone I'd trust and someone I wouldn't. Me? {safe}. That's my week.\"",
                    Dishonest = "\"I trust {close}. I don't trust {worry}. Me? Fine this week. Now you know what I know.\"",
                },
            },
            new TalkRow
            {
                Id = "star.st.threelines", Pool = TalkPools.Star, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(drying dishes beside you, voice low) \"Keep drying. My week in three lines. You get the lines, not the reasons.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Three lines. Go.\"",
                        Lands = "\"Good. No reasons. Here.\"",
                        Even = "\"Three lines, then.\"",
                        Grates = "\"You'll get two. You rushed me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"The reasons are the interesting part.\"",
                        Lands = "\"They are. That's why they're mine. Lines only.\"",
                        Even = "\"Lines only. Take them.\"",
                        Grates = "\"And now you get one line.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Can I have the reasons for a cookie?\"",
                        Lands = "\"Ha. Lines for free, reasons never. Here.\"",
                        Even = "\"No cookies. Lines.\"",
                        Grates = "\"Not a cookie conversation. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"Line one: {close}. Line two: watch {worry}. Line three: me? {safe}.\"",
                    Neutral = "\"Line one: I've got people. Line two: someone's circling. Line three: me? {safe}.\"",
                    Dishonest = "\"Line one: {close}. Line two: watch {worry}. Line three: me? Nobody's looking my way.\"",
                },
            },
        };
    }
}
