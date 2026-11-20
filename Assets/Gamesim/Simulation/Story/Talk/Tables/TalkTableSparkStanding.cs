namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the spark pool's standing talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableSparkStanding
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "spark.st.laundry", Pool = TalkPools.Spark, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(pulling you into the laundry room, shutting the door with a hip) \"You want the update? I've been dying to tell someone the update.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Tell me. All of it.\"",
                        Lands = "\"All of it. I've been holding it in all day.\"",
                        Even = "\"Most of it. Here.\"",
                        Grates = "\"All of it, says you. You get the headlines.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Who's in trouble? Just say it.\"",
                        Lands = "\"Straight to the gossip. I love that. Okay.\"",
                        Even = "\"Pushy. Fine. Quick.\"",
                        Grates = "\"Don't rush me. You get the short one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Only if it helps us both.\"",
                        Lands = "\"It does. That's why you're in the laundry room.\"",
                        Even = "\"Maybe. Here's the update.\"",
                        Grates = "\"It's an update, not a contract. Short version.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"I'm closest to {close}. I'd trust them with my bunk. {worry} makes me nervous. And me? {safe}.\"",
                    Neutral = "\"I've got my people. There's someone I keep an eye on. And me? {safe}. That's the update, more or less.\"",
                    Dishonest = "\"I'm closest to {close}. I'd trust them with my bunk. {worry} makes me nervous. And me? I'm fine.\"",
                },
            },
            new TalkRow
            {
                Id = "spark.st.feet", Pool = TalkPools.Spark, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(on the yard couch, stealing half your blanket without asking) \"We're friends now, so I'm telling you where I'm at. Don't make a face.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"No face. Look. Very neutral face.\"",
                        Lands = "\"Worst neutral face I've ever seen. I love it. Okay.\"",
                        Even = "\"Fine. Keep that face.\"",
                        Grates = "\"That's a face. Never mind. Short one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Good. I need to know who's with who.\"",
                        Lands = "\"You'll get it. You're on the list of who.\"",
                        Even = "\"Okay. Here's who's with who.\"",
                        Grates = "\"It's a friend thing, not a briefing. Short one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only if you want to.\"",
                        Lands = "\"I want to. That's new for me.\"",
                        Even = "\"I do. Mostly.\"",
                        Grates = "\"Don't be so careful with me. Okay. Short.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"{close} is my person in here. {worry} worries me, a lot. And me? {safe}. Now you know.\"",
                    Neutral = "\"There's someone I'd go to the end with. Someone I'm careful around. Me? {safe}. That's what I've got.\"",
                    Dishonest = "\"{close} is my person in here. {worry} worries me, a lot. And me? I'm good. Now you know.\"",
                },
            },
            new TalkRow
            {
                Id = "spark.st.bunks", Pool = TalkPools.Spark, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(whispering from the floor between the bunks, after lights out) \"Can't sleep. Want the real version of my week? It's a lot.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"The real version. Names.\"",
                        Lands = "\"Names. Okay. You're the only one getting them.\"",
                        Even = "\"Some names. Keep your voice down.\"",
                        Grates = "\"Not so loud. Fine. Quick.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Whatever you want to tell me.\"",
                        Lands = "\"Okay. Here's the real one.\"",
                        Even = "\"Okay. Some of it.\"",
                        Grates = "\"Don't just let me ramble. Fine. Here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Real version. Not the one you tell everyone.\"",
                        Lands = "\"Fair. The real one.\"",
                        Even = "\"Okay. The real-ish one.\"",
                        Grates = "\"I don't tell everyone anything. Fine.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"If it came to it, I'd pick {close}. {worry} keeps me up. And honestly? {safe}.\"",
                    Neutral = "\"If it came to it, I know who I'd pick. Someone keeps me up. Me? {safe}. Ask me next week.\"",
                    Dishonest = "\"If it came to it, I'd pick {close}. {worry} keeps me up. And honestly? I'm okay.\"",
                },
            },
            new TalkRow
            {
                Id = "spark.st.checkin", Pool = TalkPools.Spark, Kind = TalkKind.Standing, Layer = 4, Priority = 70,
                When = new TalkGate { Layer = 4 },
                Open = "(on the counter at midnight, swinging their legs, eating cereal) \"Weekly check-in. You're one of the only people I do this with. Me first.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Go. Then I'll know where my vote goes.\"",
                        Lands = "\"Smart. Okay, here's where I am.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"It's a check-in, not a deal. Fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"No spin this time.\"",
                        Lands = "\"No spin. Promise. Here.\"",
                        Even = "\"Minimal spin.\"",
                        Grates = "\"I don't spin. Wow. Fine. Short.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Go. I'm listening.\"",
                        Lands = "\"I know you are. That's why you get this.\"",
                        Even = "\"Okay. Here's mine.\"",
                        Grates = "\"Don't do the nod. Okay. Quick.\"",
                    },
                },
                Standing = new TalkStandingLines
                {
                    Honest = "\"Me and {close}: solid. {worry}: scary. Me overall? {safe}. Your turn.\"",
                    Neutral = "\"I've got someone solid. Someone's scary. Overall? {safe}. Your turn, and be more specific than me.\"",
                    Dishonest = "\"Me and {close}: solid. {worry}: scary. Me overall? Fine. Your turn.\"",
                },
            },
        };
    }
}
