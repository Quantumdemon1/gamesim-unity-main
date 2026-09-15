// Generated from src/systems/diary-room-system.ts; SHA-256 1df8fca2eb0070f725a67c2d333be674ee6928d801f2e40df9b01b5772077a36
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    internal static class WebDiaryRoomCatalog
    {
        internal const string EvicteeToken = "__GAMESIM_EVICTED_NAME__";

        internal static WebDiaryEvent PostEviction() => new WebDiaryEvent
        {
            narrative = "The house is quiet after __GAMESIM_EVICTED_NAME__'s eviction. You sit in the diary room chair. The camera's red light blinks. How do you really feel about what happened?",
            choices = new List<WebDiaryChoice>
            {
                new WebDiaryChoice { id = "remorseful", text = "\"I feel terrible about what happened. __GAMESIM_EVICTED_NAME__ trusted me.\"", persona = "Remorseful",
                    description = "Show genuine guilt — the jury will remember your humanity", effects = new WebDiaryEffects { juryDelta = 5, reputationDelta = -2 } },
                new WebDiaryChoice { id = "ruthless", text = "\"One down. That's what happens when you come for me.\"", persona = "Ruthless",
                    description = "Own your game — intimidate the house but risk jury votes", effects = new WebDiaryEffects { juryDelta = -5, reputationDelta = 3 } },
                new WebDiaryChoice { id = "calculated", text = "\"I need to lay low. Nobody can know I orchestrated that.\"", persona = "Calculated",
                    description = "Stay in the shadows — reduced targeting but lonely at the top", effects = new WebDiaryEffects { stealthModifier = 3, reputationDelta = 0 } }
            }
        };

        internal static WebDiaryEvent Nominated() => new WebDiaryEvent
        {
            narrative = "You just got nominated. The block is a lonely place. How do you respond?",
            choices = new List<WebDiaryChoice>
            {
                new WebDiaryChoice { id = "social", text = "\"I need to rally my friends. Time to call in every favor.\"", persona = "Social Butterfly",
                    description = "Campaign hard — leverage your social connections", effects = new WebDiaryEffects { socialBonus = 2, juryDelta = 2 } },
                new WebDiaryChoice { id = "strategic", text = "\"I need to win that Veto. Nothing else matters.\"", persona = "Ruthless",
                    description = "Channel your anger into competition focus", effects = new WebDiaryEffects { competitionBonus = 2, reputationDelta = 1 } },
                new WebDiaryChoice { id = "calm", text = "\"Whatever happens, happens. I just need to stay calm.\"", persona = "Neutral",
                    description = "Keep a level head — steady play keeps you off the radar", effects = new WebDiaryEffects { stealthModifier = 1, socialBonus = 1 } }
            }
        };

        internal static WebDiaryEvent NotNominated() => new WebDiaryEvent
        {
            narrative = "Nominations just happened. The house is buzzing. What's your move?",
            choices = new List<WebDiaryChoice>
            {
                new WebDiaryChoice { id = "social", text = "\"I should check in on the nominees. Build some bridges.\"", persona = "Social Butterfly",
                    description = "Show empathy — strengthen bonds when others are vulnerable", effects = new WebDiaryEffects { socialBonus = 2, juryDelta = 2 } },
                new WebDiaryChoice { id = "strategic", text = "\"This is my chance to cut a deal while people are desperate.\"", persona = "Calculated",
                    description = "Exploit the chaos for strategic advantage", effects = new WebDiaryEffects { competitionBonus = 2, reputationDelta = -1 } },
                new WebDiaryChoice { id = "calm", text = "\"Whatever happens, happens. I just need to stay calm.\"", persona = "Neutral",
                    description = "Keep a level head — steady play keeps you off the radar", effects = new WebDiaryEffects { stealthModifier = 1, socialBonus = 1 } }
            }
        };

        internal static WebDiaryEvent Generic() => new WebDiaryEvent
        {
            narrative = "Another day in the Big Brother house. The walls feel like they're closing in. You sit in the diary room and reflect on your game so far.",
            choices = new List<WebDiaryChoice>
            {
                new WebDiaryChoice { id = "social", text = "\"I need to build more relationships. Connections win this game.\"", persona = "Social Butterfly",
                    description = "Focus on bonding — unlock social opportunities", effects = new WebDiaryEffects { socialBonus = 2, juryDelta = 1 } },
                new WebDiaryChoice { id = "strategic", text = "\"I need to start making big moves. Time to play harder.\"", persona = "Ruthless",
                    description = "Shift into aggressive mode — risky but rewarding", effects = new WebDiaryEffects { competitionBonus = 1, reputationDelta = 2 } }
            }
        };
    }
}
