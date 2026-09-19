using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The people a season can be cast from: two rosters, each houseguest carrying an archetype, a
    /// category, two traits and the line they open a conversation with.
    ///
    /// <para>This is the pool the reference build's "Choose Your Houseguest" screen draws from, and
    /// the thing this project had no equivalent of. <see cref="ContentCatalog"/> hardcodes one
    /// scenario of six, so even after the six-contestant rule came out of the rules themselves,
    /// nothing could ask for a different house — there was no second cast to ask for.</para>
    ///
    /// <para><b>Provenance, because it is mixed.</b> Every name, archetype and category is the
    /// reference build's, as is every trait pair shown on a card in its screenshots — Dan is
    /// Strategic and Manipulative, Dr. Will is Charming and Deceptive, Janelle is Competitive and
    /// Social. Ages and occupations are its own for the nine cards that showed them and authored
    /// here for the rest. Motives, pronouns and room homes are authored throughout, which is the
    /// standing those fields already have in <see cref="ContentCatalog"/>.</para>
    ///
    /// <para>The five regular-season templates that this project already ships are reproduced field
    /// for field, so casting a season from templates and starting the shipped scenario produce the
    /// same five houseguests rather than two subtly different versions of them. A test pins it.</para>
    ///
    /// <para>Every trait named is one of the seventeen in <see cref="WebTraits"/>, so a houseguest
    /// built from a template gets real stat boosts rather than decorative words. That is also
    /// asserted, because a typo would silently produce someone with no boosts at all.</para>
    /// </summary>
    public static class CastTemplates
    {
        /// <summary>Which roster a template belongs to.</summary>
        public enum Roster { Regular, AllStars }

        /// <summary>The chip shown above the grid for "no filter".</summary>
        public const string AllCategories = "All";

        /// <summary>The filter chips the reference build offers above the grid, after "All".</summary>
        public static readonly string[] Categories =
            { "Strategist", "Competitor", "Socialite", "Wildcard", "Underdog" };

        public sealed class Template
        {
            public string Id, Name, Archetype, Category, Occupation, Pronouns, HomeRoom, Motive;

            /// <summary>
            /// The rest of the card, from the reference build's own cast table.
            ///
            /// <para>These were blank here for a long time on the reasoning that the cast table had
            /// never held them. That was true of <i>this</i> table and wrong about the source:
            /// <c>src/data/character-templates.ts</c> carries a hometown and a bio for every
            /// houseguest, and has all along.</para>
            /// </summary>
            public string Hometown, Bio;
            public int Age;
            public Roster Roster;
            public string[] Traits;

            /// <summary>The card's second line: archetype, age and job, skipping anything absent.</summary>
            public string CardLine
            {
                get
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(Archetype)) parts.Add(Archetype);
                    if (Age > 0) parts.Add(Age.ToString());
                    if (!string.IsNullOrEmpty(Occupation)) parts.Add(Occupation);
                    return string.Join(" · ", parts);
                }
            }
        }

        private static Template Make(Roster roster, string id, string name, string archetype,
            string category, int age, string occupation, string pronouns, string homeRoom,
            string motive, string hometown, string bio, string primaryTrait, string secondaryTrait)
            => new Template
            {
                Roster = roster, Id = id, Name = name, Archetype = archetype, Category = category,
                Age = age, Occupation = occupation, Pronouns = pronouns, HomeRoom = homeRoom,
                Motive = motive, Hometown = hometown, Bio = bio,
                Traits = new[] { primaryTrait, secondaryTrait },
            };

        private static readonly Template[] All =
        {
            // ---------------------------------------------------------------- regular season
            Make(Roster.Regular, "alex-chen", "Alex Chen", "The Mastermind", "Strategist",
                28, "Marketing Executive", "he/him", "Private",
                "Run the house from one seat behind it, and never be the name anyone says out loud.",
                "San Francisco, CA",
                "Strategic mastermind who excels at social manipulation and long-term planning. Always three steps ahead.",
                "Strategic", "Social"),
            Make(Roster.Regular, "emma-brown", "Emma Brown", "The Scientist", "Strategist",
                29, "Scientist", "she/her", "Bedroom",
                "Test every claim she hears against what she has already seen before acting on it.",
                "Newark, NJ",
                "Brilliant mind with a flair for the dramatic. Uses her analytical skills to outthink the competition while keeping everyone guessing.",
                "Analytical", "Strategic"),
            Make(Roster.Regular, "jordan-taylor", "Jordan Taylor", "The Charmer", "Socialite",
                31, "Sales Representative", "he/him", "Living",
                "Be everyone's second-favourite person, because nobody targets their second favourite.",
                "Chicago, IL",
                "Charismatic charmer who can talk anyone into anything. Will do whatever it takes to win.",
                "Social", "Sneaky"),
            Make(Roster.Regular, "casey-wilson", "Casey Wilson", "The Party Animal", "Wildcard",
                24, "Bartender", "she/her", "Living",
                "Keep conversations light, compare what people say, and avoid becoming the obvious target.",
                "New Orleans, LA",
                "Life of the party with a surprisingly sharp strategic mind. Nobody suspects the fun one.",
                "Social", "Strategic"),
            Make(Roster.Regular, "riley-johnson", "Riley Johnson", "The Brainiac", "Underdog",
                29, "Software Engineer", "he/him", "Bedroom",
                "Turn careful observation into one reliable partnership and a well-timed competition win.",
                "Seattle, WA",
                "Analytical genius who calculates every move. May struggle socially but never in puzzles.",
                "Analytical", "Strategic"),
            Make(Roster.Regular, "jamie-roberts", "Jamie Roberts", "The Caregiver", "Socialite",
                27, "Nurse", "she/her", "Kitchen",
                "Protect people who treat her honestly while keeping the courage to make her own move.",
                "Boston, MA",
                "Empathetic caregiver who everyone trusts. Not afraid to make bold moves when necessary.",
                "Emotional", "Strategic"),
            Make(Roster.Regular, "quinn-martinez", "Quinn Martinez", "The Influencer", "Wildcard",
                25, "Social Media Influencer", "she/her", "Living",
                "Be at the centre of every story the house tells, and control how each one is told.",
                "Los Angeles, CA",
                "Fame-seeking manipulator who plays for the cameras. Will create drama for entertainment.",
                "Confrontational", "Social"),
            Make(Roster.Regular, "avery-thompson", "Avery Thompson", "The Protector", "Competitor",
                32, "Police Officer", "he/him", "Yard",
                "Pick the people worth standing in front of, and then actually stand in front of them.",
                "Dallas, TX",
                "Strong-willed protector with unwavering loyalty. Once you have their trust, they never betray.",
                "Loyal", "Competitive"),
            Make(Roster.Regular, "taylor-kim", "Taylor Kim", "The Firebrand", "Competitor",
                27, "Fitness Instructor", "she/her", "Yard",
                "Prove she can win under pressure and earn safety without surrendering her independence.",
                "Portland, OR",
                "Competitive and disciplined but has a short fuse. Never back down from a challenge.",
                "Competitive", "Confrontational"),
            Make(Roster.Regular, "sam-williams", "Sam Williams", "The Leader", "Strategist",
                34, "Restaurant Owner", "he/him", "Kitchen",
                "Give the house a plan it can follow, and be the one holding it when it works.",
                "Nashville, TN",
                "Natural born leader who builds alliances through genuine connections and strategic vision.",
                "Strategic", "Loyal"),
            Make(Roster.Regular, "blake-peterson", "Blake Peterson", "The Shadow", "Underdog",
                26, "Architect", "he/him", "Private",
                "Say little, be counted on by both sides, and still be here when one of them is not.",
                "Denver, CO",
                "Quiet observer who strikes at the perfect moment. Sees everything but reveals nothing.",
                "Analytical", "Sneaky"),
            Make(Roster.Regular, "maya-hassan", "Maya Hassan", "The Diplomat", "Strategist",
                30, "Lawyer", "she/her", "Private",
                "Build a dependable voting partnership without making promises she cannot keep.",
                "New York, NY",
                "Sophisticated strategist with impeccable social skills. Every word is carefully chosen.",
                "Strategic", "Social"),
            // ---------------------------------------------------------------- all-stars
            Make(Roster.AllStars, "dan-gheesling", "Dan Gheesling", "The Funeral Director", "Strategist",
                35, "Football Coach", "he/him", "Private",
                "Get people to carry his game for him, and make each of them believe it was their idea.",
                "Dearborn, MI",
                "Two-time finalist and mastermind of the legendary \"Dan's Funeral.\" Manipulates through loyalty and calculated betrayals that leave houseguests stunned.",
                "Strategic", "Manipulative"),
            Make(Roster.AllStars, "dr-will-kirby", "Dr. Will Kirby", "The Puppet Master", "Strategist",
                42, "Dermatologist", "he/him", "Living",
                "Tell everyone the truth about lying to them, and watch them keep him anyway.",
                "Los Angeles, CA",
                "The original puppet master who won without winning a single competition. His charm and deception are unmatched in Big Brother history.",
                "Charming", "Deceptive"),
            Make(Roster.AllStars, "derrick-levasseur", "Derrick Levasseur", "The Undercover Boss", "Strategist",
                40, "Police Sergeant", "he/him", "Kitchen",
                "Never let the house see the version of him that is actually running the season.",
                "Providence, RI",
                "Undercover cop who controlled the entire house without ever being nominated. His analytical mind dissects every social dynamic.",
                "Analytical", "Manipulative"),
            Make(Roster.AllStars, "janelle-pierzina", "Janelle Pierzina", "The Comp Queen", "Competitor",
                43, "Real Estate Agent", "she/her", "Yard",
                "Take safety off the board by winning it, every week, until nobody can afford her.",
                "Minneapolis, MN",
                "Three-time player and undisputed competition queen. Her fierce loyalty and athletic dominance make her the ultimate competitor.",
                "Competitive", "Social"),
            Make(Roster.AllStars, "cody-calafiore", "Cody Calafiore", "The Hitman's Partner", "Competitor",
                33, "Sales Executive", "he/him", "Yard",
                "Find one person worth trusting completely, and win everything standing beside them.",
                "Howell, NJ",
                "Returned to dominate All-Stars with a perfect game. His unwavering loyalty and competitive fire make him a dual threat.",
                "Loyal", "Competitive"),
            Make(Roster.AllStars, "danielle-reyes", "Danielle Reyes", "The Black Widow", "Strategist",
                52, "Nonprofit Director", "she/her", "Bedroom",
                "Read the room better than anyone in it, and keep every reading to herself.",
                "Edmonds, WA",
                "Pioneer strategist who played before the jury could watch footage. Her intuitive reads on people are legendary.",
                "Strategic", "Intuitive"),
            Make(Roster.AllStars, "vanessa-rousso", "Vanessa Rousso", "The Poker Player", "Wildcard",
                41, "Professional Poker Player", "she/her", "Private",
                "Work out everyone's odds out loud, then bet against the version they showed her.",
                "Las Vegas, NV",
                "Poker pro who brought game theory to Big Brother. Emotional on the surface but calculating every probability underneath.",
                "Analytical", "Emotional"),
            Make(Roster.AllStars, "tyler-crispen", "Tyler Crispen", "The Surfer Strategist", "Socialite",
                28, "Lifeguard", "he/him", "Yard",
                "Look like the least dangerous person here for as long as that stays useful.",
                "Hilton Head, SC",
                "Laid-back surfer who charmed his way through the house while running multiple alliances simultaneously. Never looks like a threat.",
                "Charming", "Strategic"),
            Make(Roster.AllStars, "chelsie-baham", "Chelsie Baham", "The Assassin", "Underdog",
                27, "Nonprofit Coordinator", "she/her", "Kitchen",
                "Be trusted by every side of the house, and pick which side finds out last.",
                "Rancho Cucamonga, CA",
                "Quiet force who built deep connections and struck at exactly the right moments. Her social awareness flies under every radar.",
                "Strategic", "Social"),
            Make(Roster.AllStars, "rachel-reilly", "Rachel Reilly", "The Vegas Showgirl", "Wildcard",
                39, "Entertainment Host", "she/her", "Living",
                "Take every shot in front of her, loudly, and make the house deal with the noise.",
                "Las Vegas, NV",
                "Brash, confrontational, and fiercely competitive. Her catchphrase \"No one gets between me and my man!\" echoes through BB history.",
                "Confrontational", "Competitive"),
            Make(Roster.AllStars, "xavier-prather", "Xavier Prather", "The Cookout Captain", "Competitor",
                29, "Attorney", "he/him", "Kitchen",
                "Hold one alliance together all the way to the end without ever raising his voice.",
                "Milwaukee, WI",
                "Cool and collected strategist who led the historic Cookout alliance to victory. Balances fierce loyalty with ruthless gameplay.",
                "Strategic", "Loyal"),
            Make(Roster.AllStars, "jun-song", "Jun Song", "The Floater Queen", "Underdog",
                45, "Chef & Writer", "she/her", "Kitchen",
                "Stay out of every war, be nobody's threat, and outlast all of the people who were.",
                "New York, NY",
                "The original floater queen who perfected the art of playing the middle. Sneaky, analytical, and always three conversations ahead.",
                "Sneaky", "Analytical"),
        };

        public static IReadOnlyList<Template> Everyone => All;

        public static IEnumerable<Template> In(Roster roster) => All.Where(t => t.Roster == roster);

        /// <summary>One roster, narrowed to a category chip. "All", empty or unknown shows everyone.</summary>
        public static IEnumerable<Template> Filter(Roster roster, string category)
            => In(roster).Where(t => string.IsNullOrEmpty(category)
                || string.Equals(category, AllCategories, StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, t.Category, StringComparison.OrdinalIgnoreCase));

        public static Template Find(string id)
            => All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

        /// <summary>The name shown on a roster's tab.</summary>
        public static string RosterName(Roster roster)
            => roster == Roster.AllStars ? "All-Stars" : "Regular Season";

        /// <summary>
        /// A contestant built from a template, with the trait boosts already applied.
        ///
        /// <para>Stats come from <see cref="WebTraits.CreateStats"/> rather than being stored in the
        /// table, so a houseguest cast here and the same person in the shipped scenario are built by
        /// one formula.</para>
        /// </summary>
        public static ContestantState ToContestant(Template template, bool isPlayer)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            return new ContestantState
            {
                id = template.Id,
                name = template.Name,
                pronouns = template.Pronouns,
                archetype = template.Archetype,
                age = template.Age,
                occupation = template.Occupation,
                homeRoom = template.HomeRoom,
                motive = template.Motive,
                hometown = template.Hometown,
                bio = template.Bio,
                isPlayer = isPlayer,
                status = ContestantStatus.Active,
                traits = new List<string>(template.Traits ?? Array.Empty<string>()),
                stats = WebTraits.CreateStats(template.Traits),
            };
        }
    }
}
