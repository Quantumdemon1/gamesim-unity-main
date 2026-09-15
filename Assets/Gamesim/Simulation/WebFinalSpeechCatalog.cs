// Generated from src/utils/speech-generator.ts; SHA-256 d8cbe66d3ddfae67fef4397d5e394094aa1771624d942f825813540ab9b2c550
using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    internal static class WebFinalSpeechCatalog
    {
        internal static readonly Dictionary<string, string[]> Finale = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "cerebral", new[] { "Every move I made was calculated. I studied the dynamics, I anticipated the blindsides, and I adapted my strategy week by week. I earned this seat through intelligence.", "My game was built on foresight. While others reacted, I planned. I positioned myself to survive every single eviction, and that's why I'm sitting here tonight.", "I played the most complete game in this house. I balanced relationships with strategy, and I always thought three steps ahead. That deserves your vote." } },
            { "social", new[] { "The bonds I formed carried me here. I played with my heart, and I built trust with nearly everyone in this house. My social game was my superpower.", "I won this game by being someone people wanted to work with. I was genuine, I was kind, and I built real relationships. That's how you survive Big Brother.", "My game wasn't about backstabbing or manipulation — it was about connection. I earned people's trust and I honored it. I'm proud of the game I played." } },
            { "aggressive", new[] { "I dominated this game. I won competitions when I needed to, I made bold moves, and I never backed down from a fight. Champions play to win, and that's what I did.", "My game was all about power. I won HoH, I won Veto, and I controlled the direction of this house. If you respect gameplay, you respect what I did.", "I played this game harder than anyone. Every competition, every eviction night — I brought everything I had. I didn't coast. I conquered." } },
            { "sneaky", new[] { "You might not have seen my game, and that's exactly why it worked. I pulled strings from the shadows, and not a single person ever figured out my full strategy.", "I played the most underrated game in this house. While others were loud, I was surgical. Every whispered conversation, every misdirection — it all led to this moment.", "My game was invisible by design. I let others take the heat while I positioned myself perfectly. That takes more skill than winning any competition." } },
            { "emotional", new[] { "This journey changed my life. I came in unsure of myself and I'm leaving as someone who survived the most intense experience imaginable. I played with my whole heart.", "I know my emotions showed, but that's because this game meant everything to me. I fought through tears, through doubt, and I'm still standing. That's strength.", "Being here was a dream, and I gave it absolutely everything. My passion drove my game. I hope you can see how much this means to me." } }
        };

        internal static readonly Dictionary<string, string[]> Stories = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "social_drama", new[] { " And what happened between me and {npc} showed my true character.", " After everything that went down with \"{storyline},\" I think you all saw who I really am." } },
            { "power_play", new[] { " I made bold moves when it counted — \"{storyline}\" proved that.", " When the house needed someone to step up during \"{storyline},\" I was the one who did." } },
            { "personal_growth", new[] { " \"{storyline}\" taught me something about myself that I'll carry beyond this house.", " Going through \"{storyline}\" showed me I'm stronger than I thought." } },
            { "survival", new[] { " Surviving \"{storyline}\" was one of the hardest things I've done in this game.", " \"{storyline}\" nearly broke me, but I'm still here." } },
            { "secret_scheme", new[] { " There are things about my game you don't even know yet.", " I played moves behind the scenes that changed the course of this game." } }
        };

        internal static readonly string[] Betrayal = new[] { " And yes, I know a deal was broken. I won't pretend that didn't happen.", " Some promises were made and broken in this house — but I own my decisions.", " I know trust was damaged along the way, but that's the nature of this game." };

        internal static readonly Dictionary<string, string[]> Brag = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "aggressive", new[] { " My {wins} competition wins speak for themselves.", " I proved myself in {wins} competitions — that's not luck, that's skill." } },
            { "cerebral", new[] { " My {wins} wins weren't accidents — each one was part of a larger plan." } },
            { "social", new[] { " I may have {wins} wins, but my real victories were the friendships I built." } },
            { "sneaky", new[] { " Some people brag about wins. I won {wins} times, but my real game happened between competitions." } },
            { "emotional", new[] { " Every one of my {wins} wins meant the world to me." } }
        };
    }
}
