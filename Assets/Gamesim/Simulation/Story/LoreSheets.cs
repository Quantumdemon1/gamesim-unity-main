using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Authored lore for the fictional regular roster. Every sheet agrees with its public card -
    /// name, age, hometown, job, both traits, the motive and the bio - and builds on it: the card
    /// is what the house sees on day one, the sheet is what you find out by the end.
    ///
    /// <para>Depth is the reveal ladder (20 §3.7): 1 comes up in a first conversation, 2 at rapport
    /// three, 3 at rapport six after a finished personal story, 4 only through an arc. A respects
    /// fact makes its approach resonate once known (+1); a hot-button makes its approach grate
    /// (−1), and bites before it is known. Home, the unforgivable thing, romance and the secret are
    /// personal; everything else is how they play.</para>
    /// </summary>
    public static partial class Lore
    {
        private static IEnumerable<Sheet> AuthoredSheets()
        {
            yield return Written("jamie-roberts", "Emotional", "closed", "hurt", "her-own-move",
                P("origin", Facets.Origin, 1, "Grew up in Dorchester, the oldest of four. Her mom worked nights, so Jamie made the school lunches."),
                Personal("home", Facets.Home, 2, "Still lives in a two-family house with her mom downstairs. Her boyfriend is a Boston firefighter; if she wins HoH, the letter is from him."),
                P("work", Facets.Work, 1, "ER nurse, twelve-hour night shifts. She still wakes at 3 a.m. and wipes down the kitchen."),
                P("respects", Facets.Respects, 2, "Someone who owns it when they hurt her. Tell her you're sorry and mean it, and she'll remember.", Candid, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being lied to about a vote."),
                P("hot-emotional", Facets.HotButton, 2, "Being called too emotional to play this game.", Calculated, -1, "game talk"),
                P("hot-mom", Facets.HotButton, 2, "Being called the house mom. Wasted food runs a close second.", Hardball, -1, "the kitchen"),
                P("comfort", Facets.Comfort, 1, "The kitchen. A stressed Jamie is at the stove, and whoever eats what she cooks is safe for a day."),
                P("goal", Facets.Goal, 2, "To make one move that is hers and nobody else's, and be seen making it."),
                P("conflict", Facets.ConflictStyle, 2, "Warm until she's lied to, then loud and in public. She'll hug you afterwards and still vote you out."),
                Romance("romance", 1, "Closed: the firefighter."),
                Personal("secret", Facets.Secret, 4, "A superfan. She has watched every season since she was twelve; her little brother Danny applied four times and never got the call, and she is playing his notebook. She tells the house she has \"seen a couple of seasons\"."));

            yield return Written("quinn-martinez", "Confrontational", "strategic", "loud", "centre-of-the-story",
                P("origin", Facets.Origin, 1, "Grew up in Riverside and moved to LA at nineteen with one suitcase and a ring light."),
                Personal("home", Facets.Home, 2, "A studio apartment in Echo Park she films everything in. Her mom watches the feeds and texts her manager."),
                P("work", Facets.Work, 1, "Four hundred thousand followers for \"brutally honest\" makeup reviews. She knows where every camera in the house is."),
                P("respects", Facets.Respects, 2, "Someone who says it to her face.", Hardball, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being called fake, or being ignored. Boring is the only thing she can't survive."),
                P("hot-calm", Facets.HotButton, 1, "\"Calm down.\" Also \"clout-chaser\", and people who whisper about her in the storage room.", Yield, -1, "calm down"),
                P("comfort", Facets.Comfort, 1, "The living-room couch, facing the cameras."),
                P("goal", Facets.Goal, 2, "To be at the centre of every story the house tells, and to control how each one is told."),
                P("conflict", Facets.ConflictStyle, 1, "Public and on camera. With two or more watching she escalates; alone, she backs down."),
                Romance("romance", 2, "Open, strategically: \"a showmance is content.\""),
                Personal("secret", Facets.Secret, 4, "Her manager told her the brand deal only renews if she makes jury."));

            yield return Written("alex-chen", "Strategic", "strategic", "cold", "run-it-quietly",
                P("origin", Facets.Origin, 1, "Grew up over his parents' dry cleaner in San Francisco's Richmond District, doing the books at the counter by twelve."),
                Personal("home", Facets.Home, 2, "Shares a flat in the Mission with his younger sister, who is minding his cat and, he suspects, reading his mail. If he wins HoH, the letter is from her."),
                P("work", Facets.Work, 1, "Runs brand campaigns for a tech firm. Knows exactly how a story gets sold, and to whom."),
                P("respects", Facets.Respects, 2, "A plan with a reason he can follow. Show him the working and he will listen to anything.", Calculated, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being made to look foolish in front of the house."),
                P("hot-exposed", Facets.HotButton, 2, "Being called the mastermind out loud. He wants the credit later, never now.", Candid, -1, "exposure"),
                P("comfort", Facets.Comfort, 1, "A quiet corner where he can see who goes up to the HoH room and who comes down."),
                P("goal", Facets.Goal, 2, "To steer every week without his name ever being the one said at the ceremony."),
                P("conflict", Facets.ConflictStyle, 1, "Never raises his voice. Goes quiet, smiles, and moves your name up his list."),
                Romance("romance", 2, "Open in theory. He would call a showmance a strategic liability and mean it."),
                Personal("secret", Facets.Secret, 4, "He left his last job after a campaign leaked to a rival firm, and he was the one who leaked it."));

            yield return Written("emma-brown", "Analytical", "closed", "cold", "test-everything",
                P("origin", Facets.Origin, 1, "Grew up in Newark's Ironbound, the kid who took the toaster apart to see how it worked, and put it back better."),
                Personal("home", Facets.Home, 2, "Lives with the grandmother who raised her, who still calls her by her middle name."),
                P("work", Facets.Work, 1, "A research chemist. Runs every experiment three times before she believes the result."),
                P("respects", Facets.Respects, 2, "Evidence. Show her the vote count and she will follow it anywhere.", Calculated, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "A lie told with a straight face after she has asked a direct question."),
                P("hot-rushed", Facets.HotButton, 2, "Being rushed. Push her to decide right now and the answer is no.", Bold, -1, "pressure"),
                P("comfort", Facets.Comfort, 1, "The bedroom, cross-legged on the bed, running lists in her head."),
                P("goal", Facets.Goal, 2, "To be right about everyone before anyone is right about her."),
                P("conflict", Facets.ConflictStyle, 1, "Cold and precise: she will list, in order, every inconsistency in what you said."),
                Romance("romance", 1, "Closed: engaged to a high-school physics teacher. The ring is in her suitcase; she did not want it to be a talking point."),
                Personal("secret", Facets.Secret, 4, "She has a patent pending that could make her rich whether she wins or not, and she thinks the house would stop seeing her as an underdog if they knew."));

            yield return Written("jordan-taylor", "Social", "open", "even", "second-favourite",
                P("origin", Facets.Origin, 1, "A South Side Chicago kid, the middle of five brothers, who could talk his way into and out of anything by nine."),
                Personal("home", Facets.Home, 2, "Still does Sunday dinner at his mother's, and brings the dessert every time."),
                P("work", Facets.Work, 1, "Sells commercial printers, and has never lost a customer he got to meet in person."),
                P("respects", Facets.Respects, 2, "Someone who makes the room easier. He notices who does the work of keeping people happy.", Warm, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being frozen out. He can handle a fight; he cannot handle being ignored."),
                P("hot-cornered", Facets.HotButton, 2, "Ultimatums. Back him into a corner and he smiles and walks straight out the other side.", Hardball, -1, "ultimatums"),
                P("comfort", Facets.Comfort, 1, "The living-room couch, in the middle of whatever is happening."),
                P("goal", Facets.Goal, 2, "To be everyone's second-favourite person, because nobody targets their second favourite."),
                P("conflict", Facets.ConflictStyle, 1, "Charm first, always. If that fails, he agrees with you and does what he wanted anyway."),
                Romance("romance", 1, "Single, and very aware that a showmance is the fastest way to a voting bloc."),
                Personal("secret", Facets.Secret, 4, "He has already told three different houseguests they are his number one."));

            yield return Written("casey-wilson", "Social", "open", "even", "keep-it-light",
                P("origin", Facets.Origin, 1, "Grew up in the Bywater above her aunt's bar, doing homework to a brass band through the floor."),
                Personal("home", Facets.Home, 2, "Her aunt still runs the bar, and has promised to put the finale on every screen in the place."),
                P("work", Facets.Work, 1, "Tends bar in New Orleans. Hears everybody's secrets and keeps every one."),
                P("respects", Facets.Respects, 2, "Someone who can take a joke and give one back.", Warm, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being kept around as the fun one while everyone else makes the real plans."),
                P("hot-party", Facets.HotButton, 2, "Being treated like the party girl in a strategy talk.", Calculated, -1, "being underestimated"),
                P("comfort", Facets.Comfort, 1, "The living room late at night, when the house gets loose and talkative."),
                P("goal", Facets.Goal, 2, "To keep every conversation light, compare what people say, and never become the obvious target."),
                P("conflict", Facets.ConflictStyle, 1, "Laughs it off in public, and remembers it in private."),
                Romance("romance", 1, "Single, and would not say no to a summer romance, as long as it never costs her a vote."),
                Personal("secret", Facets.Secret, 4, "She keeps a running tally of every lie anyone tells in front of her, and she has not been wrong yet."));

            yield return Written("riley-johnson", "Analytical", "closed", "cold", "one-partner",
                P("origin", Facets.Origin, 1, "Grew up in Tacoma, the kid who built his own computer at eleven and did not talk much at school."),
                Personal("home", Facets.Home, 2, "Lives alone with a very old dog called Pixel, and worries about her every day he is in here."),
                P("work", Facets.Work, 1, "Writes code for a logistics company, mostly alone, mostly at night."),
                P("respects", Facets.Respects, 2, "A reasoned move, even one against him. He can forgive a vote that had a reason.", Calculated, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being laughed at for missing a social cue."),
                P("hot-teased", Facets.HotButton, 2, "Being teased in front of people. He laughs along, and never forgets it.", Playful, -1, "teasing"),
                P("comfort", Facets.Comfort, 1, "The bedroom with the lights off, running the numbers on next week."),
                P("goal", Facets.Goal, 2, "One reliable partner, and one competition win at exactly the right moment."),
                P("conflict", Facets.ConflictStyle, 1, "Goes cold. Stops talking to you and starts counting votes."),
                Romance("romance", 2, "Not looking. He came here to win a game, not to meet anyone."),
                Personal("secret", Facets.Secret, 4, "He applied on a dare from a coworker, and has been terrified since day one that he will be the first one out."));

            yield return Written("avery-thompson", "Loyal", "closed", "hurt", "stand-in-front",
                P("origin", Facets.Origin, 1, "Grew up in Oak Cliff, the eldest of three, looking out for his brothers before anybody asked him to."),
                Personal("home", Facets.Home, 2, "His daughter is six, and made him promise to win a veto for her. If he wins HoH, the letter is in her handwriting."),
                P("work", Facets.Work, 1, "A patrol officer in Dallas. Reads a room before he walks into it."),
                P("respects", Facets.Respects, 2, "Someone who keeps their word when it costs them.", Candid, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Betraying somebody who trusted you. He will not forgive it, even when it was not done to him."),
                P("hot-threats", Facets.HotButton, 2, "Threats. Tell him what happens if he doesn't, and he will make sure it does.", Hardball, -1, "threats"),
                P("comfort", Facets.Comfort, 1, "The backyard at dawn, running laps while the house sleeps."),
                P("goal", Facets.Goal, 2, "To pick the people worth standing in front of, and then actually stand in front of them."),
                P("conflict", Facets.ConflictStyle, 1, "Steady and direct. He tells you once, calmly. There is no second time."),
                Romance("romance", 1, "Closed: married to his high-school sweetheart."),
                Personal("secret", Facets.Secret, 4, "He has promised himself he will take the first person who stands up for him in here to the end, whatever it costs him."));

            yield return Written("taylor-kim", "Competitive", "open", "loud", "win-under-pressure",
                P("origin", Facets.Origin, 1, "Grew up in Beaverton, a gymnast until a knee injury at sixteen, and has been proving she is not done ever since."),
                Personal("home", Facets.Home, 2, "Her mom runs the family's Korean restaurant and has told every regular to watch."),
                P("work", Facets.Work, 1, "Teaches strength classes at five in the morning in Portland, and loves the clients who swear at her."),
                P("respects", Facets.Respects, 2, "Someone who beats her fair and square, and says so.", Bold, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being called soft."),
                P("hot-calm", Facets.HotButton, 1, "Being told to calm down.", Yield, -1, "calm down"),
                P("comfort", Facets.Comfort, 1, "The backyard, lifting whatever the house has that counts as weights."),
                P("goal", Facets.Goal, 2, "To win under pressure and earn her safety without owing anybody for it."),
                P("conflict", Facets.ConflictStyle, 1, "Short fuse, loud, and over in ten minutes. She does not do the silent treatment."),
                Romance("romance", 2, "Open, and not subtle about it."),
                Personal("secret", Facets.Secret, 4, "The knee never healed right, and she is terrified an endurance competition will show everyone."));

            yield return Written("sam-williams", "Strategic", "closed", "even", "give-a-plan",
                P("origin", Facets.Origin, 1, "Grew up in East Nashville washing dishes in other people's kitchens, and opened his own at twenty-eight."),
                Personal("home", Facets.Home, 2, "His husband is running the restaurant while he is in here, and Sam is sure the menu is being changed."),
                P("work", Facets.Work, 1, "Owns a hot-chicken place in Nashville with a line round the block on Saturdays."),
                P("respects", Facets.Respects, 2, "A plan the house can follow. Bring him one and he'll cook for you.", Calculated, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Somebody taking credit for his plan."),
                P("hot-chaos", Facets.HotButton, 2, "Chaos for its own sake: a blow-up with no reason behind it.", Bold, -1, "chaos"),
                P("comfort", Facets.Comfort, 1, "The kitchen, feeding the house whether it asked or not."),
                P("goal", Facets.Goal, 2, "To give the house a plan it can follow, and be the one holding it when it works."),
                P("conflict", Facets.ConflictStyle, 2, "Calls a meeting. Calmly lays out what went wrong, and who did it."),
                Romance("romance", 1, "Closed: married."),
                Personal("secret", Facets.Secret, 4, "The restaurant is in trouble. He needs the prize money to keep it open, and has told nobody, not even his husband."));

            yield return Written("blake-peterson", "Analytical", "open", "cold", "counted-on-by-both",
                P("origin", Facets.Origin, 1, "Grew up in a small town outside Durango, the quiet kid who drew floor plans of houses he had never been inside."),
                Personal("home", Facets.Home, 2, "Lives with two roommates in Denver who bet him a hundred dollars he would not make it past week two."),
                P("work", Facets.Work, 1, "An architect: libraries and schools. Notices the exits in every room."),
                P("respects", Facets.Respects, 2, "Someone who can keep a secret, and knows what one is worth.", Calculated, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "Being exposed in front of the house for something he said in private."),
                P("hot-spot", Facets.HotButton, 2, "Being put on the spot. Ask him in front of everyone and you get nothing.", Bold, -1, "attention"),
                P("comfort", Facets.Comfort, 1, "Whichever room is empty."),
                P("goal", Facets.Goal, 2, "To say little, be counted on by both sides, and still be here when one of them is not."),
                P("conflict", Facets.ConflictStyle, 1, "Never argues. Agrees with you, and remembers."),
                Romance("romance", 2, "Single and shy about it, and will not be the one to make the first move."),
                Personal("secret", Facets.Secret, 4, "He has played both sides since week one, and keeps an exact map in his head of which story he told to whom."));

            yield return Written("maya-hassan", "Strategic", "closed", "cold", "dependable-partner",
                P("origin", Facets.Origin, 1, "Grew up in Bay Ridge, Brooklyn, the daughter of a cab driver and a schoolteacher, and argued her way into every school she went to."),
                Personal("home", Facets.Home, 2, "Her parents still live in the apartment she grew up in, and her father tells every passenger that his daughter is on TV."),
                P("work", Facets.Work, 1, "A litigator in New York. Has never asked a question she did not already know the answer to."),
                P("respects", Facets.Respects, 2, "A promise that says exactly what it means.", Calculated, 1),
                Personal("unforgivable", Facets.Unforgivable, 3, "A promise made with a loophole in it."),
                P("hot-flattery", Facets.HotButton, 2, "Flattery. Tell her she's brilliant and she starts wondering what you want.", Warm, -1, "flattery"),
                P("comfort", Facets.Comfort, 1, "Halfway up the stairs, where every conversation in the house passes her."),
                P("goal", Facets.Goal, 2, "A dependable voting partnership, and not one promise she cannot keep."),
                P("conflict", Facets.ConflictStyle, 2, "Cross-examines: calmly, one question at a time, until you contradict yourself."),
                Romance("romance", 1, "Not here for that, and says so on day one."),
                Personal("secret", Facets.Secret, 4, "She took a leave of absence her firm may not let her come back from. Losing is not the worst thing that could happen; going home early is."));
        }

        // ---------------------------------------------------------------- building a sheet

        private static Sheet Written(string templateId, string primaryTrait, string romance, string conflictStyle, string goal, params Fact[] facts) =>
            new Sheet
            {
                key = templateId, source = LoreSources.Authored, primaryTrait = primaryTrait, romance = romance,
                conflictStyle = conflictStyle, goal = goal,
                facts = facts.Select(f => { f.id = templateId + ":" + f.id; return f; }).ToArray(),
            };

        /// <summary>A fact about how somebody plays: fit for any register.</summary>
        private static Fact P(string id, string facet, int depth, string text, string approach = null, int sign = 0, string topic = null) =>
            new Fact { id = id, facet = facet, depth = depth, text = text, approach = approach, sign = sign, topic = topic };

        /// <summary>A fact about somebody's life outside: fictional sheets only.</summary>
        private static Fact Personal(string id, string facet, int depth, string text) =>
            new Fact { id = id, facet = facet, depth = depth, text = text, sensitivity = StoryPeople.Sensitivity.Personal };

        private static Fact Romance(string id, int depth, string text) =>
            new Fact { id = id, facet = Facets.Romance, depth = depth, text = text, sensitivity = StoryPeople.Sensitivity.Romance };
    }
}
