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
            string motive, string primaryTrait, string secondaryTrait)
            => new Template
            {
                Roster = roster, Id = id, Name = name, Archetype = archetype, Category = category,
                Age = age, Occupation = occupation, Pronouns = pronouns, HomeRoom = homeRoom,
                Motive = motive, Traits = new[] { primaryTrait, secondaryTrait },
            };

        private static readonly Template[] All =
        {
            // ---------------------------------------------------------------- regular season
            // The five drawn from this project's shipped scenario are reproduced from
            // ContentCatalog exactly: same pronouns, same room, same motive, same trait order.
            Make(Roster.Regular, "alex-chen", "Alex Chen", "The Mastermind", "Strategist",
                28, "Software Architect", "he/him", "Private",
                "Run the house from one seat behind it, and never be the name anyone says out loud.",
                "Analytical", "Manipulative"),
            Make(Roster.Regular, "emma-brown", "Emma Brown", "The Scientist", "Strategist",
                29, "Scientist", "she/her", "Bedroom",
                "Test every claim she hears against what she has already seen before acting on it.",
                "Analytical", "Strategic"),
            Make(Roster.Regular, "jordan-taylor", "Jordan Taylor", "The Charmer", "Socialite",
                27, "Sales Representative", "he/him", "Living",
                "Be everyone's second-favourite person, because nobody targets their second favourite.",
                "Charming", "Social"),
            Make(Roster.Regular, "casey-wilson", "Casey Wilson", "The Party Animal", "Socialite",
                24, "Bartender", "she/her", "Living",
                "Keep conversations light, compare what people say, and avoid becoming the obvious target.",
                "Social", "Strategic"),
            Make(Roster.Regular, "riley-johnson", "Riley Johnson", "The Brainiac", "Strategist",
                29, "Data Analyst", "he/him", "Bedroom",
                "Turn careful observation into one reliable partnership and a well-timed competition win.",
                "Analytical", "Strategic"),
            Make(Roster.Regular, "jamie-roberts", "Jamie Roberts", "The Caregiver", "Underdog",
                38, "Paediatric Nurse", "she/her", "Kitchen",
                "Protect people who treat her honestly while keeping the courage to make her own move.",
                "Emotional", "Strategic"),
            Make(Roster.Regular, "quinn-martinez", "Quinn Martinez", "The Influencer", "Socialite",
                25, "Content Creator", "they/them", "Living",
                "Be at the centre of every story the house tells, and control how each one is told.",
                "Charming", "Deceptive"),
            Make(Roster.Regular, "avery-thompson", "Avery Thompson", "The Protector", "Competitor",
                34, "Firefighter", "he/him", "Yard",
                "Pick the people worth standing in front of, and then actually stand in front of them.",
                "Loyal", "Stubborn"),
            Make(Roster.Regular, "taylor-kim", "Taylor Kim", "The Firebrand", "Competitor",
                26, "Personal Trainer", "she/her", "Yard",
                "Prove she can win under pressure and earn safety without surrendering her independence.",
                "Competitive", "Confrontational"),
            Make(Roster.Regular, "sam-williams", "Sam Williams", "The Leader", "Competitor",
                36, "Site Foreman", "he/him", "Kitchen",
                "Give the house a plan it can follow, and be the one holding it when it works.",
                "Competitive", "Loyal"),
            Make(Roster.Regular, "blake-peterson", "Blake Peterson", "The Shadow", "Wildcard",
                31, "Night Auditor", "he/him", "Private",
                "Say little, be counted on by both sides, and still be here when one of them is not.",
                "Introverted", "Sneaky"),
            Make(Roster.Regular, "maya-hassan", "Maya Hassan", "The Diplomat", "Strategist",
                31, "Mediator", "she/her", "Private",
                "Build a dependable voting partnership without making promises she cannot keep.",
                "Strategic", "Social"),

            // ---------------------------------------------------------------- all-stars
            Make(Roster.AllStars, "dan-gheesling", "Dan Gheesling", "The Funeral Director", "Strategist",
                35, "Football Coach", "he/him", "Private",
                "Get people to carry his game for him, and make each of them believe it was their idea.",
                "Strategic", "Manipulative"),
            Make(Roster.AllStars, "will-kirby", "Dr. Will Kirby", "The Puppet Master", "Strategist",
                42, "Dermatologist", "he/him", "Living",
                "Tell everyone the truth about lying to them, and watch them keep him anyway.",
                "Charming", "Deceptive"),
            Make(Roster.AllStars, "derrick-levasseur", "Derrick Levasseur", "The Undercover Boss", "Strategist",
                36, "Police Sergeant", "he/him", "Kitchen",
                "Never let the house see the version of him that is actually running the season.",
                "Strategic", "Loyal"),
            Make(Roster.AllStars, "janelle-pierzina", "Janelle Pierzina", "The Comp Queen", "Competitor",
                43, "Real Estate Agent", "she/her", "Yard",
                "Take safety off the board by winning it, every week, until nobody can afford her.",
                "Competitive", "Social"),
            Make(Roster.AllStars, "cody-calafiore", "Cody Calafiore", "The Hitman's Partner", "Competitor",
                33, "Sales Executive", "he/him", "Yard",
                "Find one person worth trusting completely, and win everything standing beside them.",
                "Loyal", "Competitive"),
            Make(Roster.AllStars, "danielle-reyes", "Danielle Reyes", "The Black Widow", "Strategist",
                52, "Nonprofit Director", "she/her", "Bedroom",
                "Read the room better than anyone in it, and keep every reading to herself.",
                "Strategic", "Intuitive"),
            Make(Roster.AllStars, "vanessa-rousso", "Vanessa Rousso", "The Poker Player", "Wildcard",
                41, "Professional Poker Player", "she/her", "Private",
                "Work out everyone's odds out loud, then bet against the version they showed her.",
                "Analytical", "Emotional"),
            Make(Roster.AllStars, "tyler-crispen", "Tyler Crispen", "The Surfer Strategist", "Strategist",
                29, "Lifeguard", "he/him", "Yard",
                "Look like the least dangerous person here for as long as that stays useful.",
                "Flexible", "Charming"),
            Make(Roster.AllStars, "chelsie-baham", "Chelsie Baham", "The Assassin", "Strategist",
                27, "Nonprofit Coordinator", "she/her", "Kitchen",
                "Be trusted by every side of the house, and pick which side finds out last.",
                "Strategic", "Social"),
            Make(Roster.AllStars, "rachel-reilly", "Rachel Reilly", "The Vegas Showgirl", "Competitor",
                38, "Cocktail Waitress", "she/her", "Living",
                "Take every shot in front of her, loudly, and make the house deal with the noise.",
                "Competitive", "Emotional"),
            Make(Roster.AllStars, "xavier-prather", "Xavier Prather", "The Cookout Captain", "Competitor",
                29, "Attorney", "he/him", "Kitchen",
                "Hold one alliance together all the way to the end without ever raising his voice.",
                "Strategic", "Loyal"),
            Make(Roster.AllStars, "jun-song", "Jun Song", "The Floater Queen", "Underdog",
                44, "Chef", "she/her", "Kitchen",
                "Stay out of every war, be nobody's threat, and outlast all of the people who were.",
                "Flexible", "Sneaky"),
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
                isPlayer = isPlayer,
                status = ContestantStatus.Active,
                traits = new List<string>(template.Traits ?? Array.Empty<string>()),
                stats = WebTraits.CreateStats(template.Traits),
            };
        }
    }
}
