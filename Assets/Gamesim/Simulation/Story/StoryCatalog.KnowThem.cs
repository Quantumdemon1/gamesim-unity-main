using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// M4, getting to know them: the one real conversation of move-in night, the photo from home,
    /// and the alumnus who knows the house is coming for them. What these teach is lore - facts the
    /// odds, the option gates and the reveal ladder read.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> KnowThem()
        {
            yield return FirstNight();
            yield return PhotoOnTheNightstand();
            yield return TheTargetOnTheirBack();
        }

        // ---------------------------------------------------------------- first-night

        /// <summary>
        /// Move-in night, after the meet-and-greet: one real conversation with one houseguest.
        /// One card and one pick, costing nothing. Worth the web's first-impression numbers on top
        /// of the introduction, landing by how the approach suits them, and you learn how they fight.
        /// </summary>
        private static ArcTemplate FirstNight() => new ArcTemplate
        {
            id = "first-night", lane = StoryLanes.Moment, eyebrow = "Move-In Night", title = "The First Night",
            origin = "web:MeetAndGreetPhase (+3/-3/+1, via WebIntroductions), rebuilt as one conversation; the lore reveal native",
            rulesVersion = StoryRules.Lore, cooldownWeeks = 100, oncePerHeadliner = false,
            cast = c => EpisodeEngine.IsFirstNight(c.state) && Npcs(c).Count > 0 ? Bind() : null,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "past-midnight", surface = StorySurfaces.Scene, venue = Living,
                    title = "The First Night", summary = "Past midnight on move-in night, there is time for one real conversation.",
                    text = "It is past midnight on the first night. The house is loud and giddy and nobody has slept, and you finally get one real conversation. Who with, and how?",
                    lapse = "early-night",
                    options = new[]
                    {
                        Opt("open-up", "Open up", Low, Warm, "Talk about home, and ask about theirs.",
                            "You talked until the lights went out, and learned how they handle a fight.",
                            Move(Player, Pick, 3), Reveal(Pick, Lore.Facets.ConflictStyle))
                            .Picks((c, y) => Npcs(c).Select(x => x.id)),
                        Opt("talk-strategy", "Talk strategy early", Medium, Calculated, "Feel out how they mean to play.",
                            "You talked game on night one. They told you more than they meant to about what they are here for.",
                            Move(Player, Pick, 1), Reveal(Pick, Lore.Facets.Goal))
                            .Picks((c, y) => Npcs(c).Select(x => x.id)),
                        Opt("keep-light", "Keep it light", Low, Playful, "Make them laugh. It's the first night.",
                            "You made them laugh until they cried, and you saw where they go to feel at home in here.",
                            Move(Player, Pick, 3), Reveal(Pick, Lore.Facets.Comfort))
                            .Picks((c, y) => Npcs(c).Select(x => x.id)),
                        Lapse("early-night", "Get some sleep", "It's a long summer.", "You went to bed early. It's a long summer."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- photo-on-the-nightstand

        private static ArcTemplate PhotoOnTheNightstand() => new ArcTemplate
        {
            id = "photo-on-the-nightstand", lane = StoryLanes.Personal, eyebrow = "Home", title = "The Photo on the Nightstand",
            origin = "web:homesick_moment (ambient-event-system.ts); the home fact native",
            rulesVersion = StoryRules.Lore, minWeek = 2, playerNeeds = StoryPeople.Sensitivity.Personal,
            startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.NomsSet },
            roles = new[] { Role("SUBJECT", StoryPeople.Sensitivity.Personal) },
            cast = c =>
            {
                var subject = Npcs(c).Where(x =>
                    {
                        var home = Lore.Facet(c.state, x.id, Lore.Facets.Home);
                        var contact = c.state.story.contacts.FirstOrDefault(k => k.npcId == x.id);
                        return home != null && !Lore.Knows(c.state, home.id) && contact != null && contact.rapport >= 3;
                    })
                    .OrderByDescending(x => Nominated(c, x.id)).ThenByDescending(x => c.Score(P(c), x.id))
                    .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return subject == null ? null : Bind().With("SUBJECT", subject.id).Headlining(subject.id);
            },
            weight = (c, b) => Nominated(c, b.Get("SUBJECT")) ? 20 : 10,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "the-photo", surface = StorySurfaces.Approach, venue = Bedroom,
                    title = "The Photo on the Nightstand", summary = "A houseguest is homesick, and does not hide it from you.",
                    text = "{SUBJECT} is sitting on the edge of the bed with a photo from home, and does not put it away when you come in.",
                    lapse = "give-space",
                    options = new[]
                    {
                        Opt("sit-with", "Sit with them", Low, Warm, "Ask about the photo, and listen.",
                            "{SUBJECT} told you about home, and you listened to all of it.",
                            Move(Player, "SUBJECT", 5), Receipt("SUBJECT", Player, StoryReceipts.HeardOut), Reveal("SUBJECT", Lore.Facets.Home)),
                        Opt("game-talk", "Bring it back to the game", Medium, Calculated, "Homesick people make mistakes. Use the moment.",
                            "You steered it back to the game, and {SUBJECT} put the photo away.",
                            Move(Player, "SUBJECT", -2), Receipt("SUBJECT", Player, StoryReceipts.Snubbed)),
                        Lapse("give-space", "Give them some space", "Some things are private.", "You gave {SUBJECT} some space."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-target-on-their-back

        /// <summary>
        /// All-Stars, early: an alumnus who won their season knows the house is coming for them.
        /// Game register only - their record is public and documented - so it may be told about a
        /// real person, and the player on an alumni card may play it.
        /// </summary>
        private static ArcTemplate TheTargetOnTheirBack() => new ArcTemplate
        {
            id = "the-target-on-their-back", lane = StoryLanes.Game, eyebrow = "All-Stars", title = "The Target on Their Back",
            origin = "native, alumni-safe: replaces legend-talk; the record read from the audited legacy sheet",
            rulesVersion = StoryRules.Lore, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.HohCrowned },
            roles = new[] { Role("BIGNAME") },
            cast = c =>
            {
                if (c.state.week > 3) return null;
                var big = Npcs(c).Where(x => StoryPeople.IsRealPerson(x) && Lore.SheetIn(c.state, x.id)?.wonSeason == true)
                    .OrderByDescending(x => c.Score(P(c), x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return big == null ? null : Bind().With("BIGNAME", big.id).Headlining(big.id);
            },
            weight = (c, b) => 20,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "big-name", surface = StorySurfaces.Approach, venue = Yard,
                    title = "The Target on Their Back", summary = "A past winner knows the house is coming for them.",
                    text = "{BIGNAME} has won this game before, and everyone in the house knows it. {BIGNAME} knows it too. \"They're all coming for me. I can feel it. Where are you on it?\"",
                    lapse = "noncommittal",
                    options = new[]
                    {
                        Opt("protect", "Promise to protect them", Medium, Warm, "Give {BIGNAME} your word: not from you.",
                            "You gave {BIGNAME} your word you would not be the one.", Promise(Player, "BIGNAME", PromiseKind.Safety), Move(Player, "BIGNAME", 4)),
                        Opt("fair", "Tell them it's fair", Low, Candid, "\"You won. That's the price.\"",
                            "You told {BIGNAME} the truth. {BIGNAME} respected that more than a promise.", Receipt("BIGNAME", Player, StoryReceipts.HeardOut)),
                        Opt("numbers", "Offer them the numbers", Medium, Calculated, "Vote together, and make the house think twice.",
                            "You and {BIGNAME} agreed to vote together. Two targets are harder to hit than one.", Deal(Player, "BIGNAME", DealKind.VoteTogether)),
                        Lapse("noncommittal", "Keep it vague", "\"We'll see how the week goes.\"", "You kept it vague."),
                    },
                },
            },
        };
    }
}
