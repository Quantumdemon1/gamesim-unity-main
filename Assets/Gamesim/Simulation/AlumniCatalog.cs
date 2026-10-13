using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Audited public game history through BB26 (2024), plus explicitly authored characterization.
    /// This is immutable content, not a season initializer: reading it never grants a deal, changes
    /// a relationship, writes a save, or draws randomness. Priors are design defaults rather than
    /// measurements of the real people. Interpersonal activation needs its own approved save rules.
    /// </summary>
    public static class AlumniCatalog
    {
        public const int CanonYear = 2024;
        public const int CanonSeason = 26;
        public const int NeutralPriorTrust = 50;
        public const string Version = "bb26-2024-v1";
        public const string DialogueProvenance = "Original fictional game dialogue; not a historical quotation.";

        public sealed class Source
        {
            public string Id { get; }
            public string Title { get; }
            public string Url { get; }
            public int PublishedYear { get; }

            internal Source(string id, string title, string url, int publishedYear)
            {
                Id = id;
                Title = title;
                Url = url;
                PublishedYear = publishedYear;
            }
        }

        public sealed class Fact
        {
            public string Id { get; }
            public string Show { get; }
            public int Season { get; }
            public int Year { get; }
            public string Summary { get; }
            public IReadOnlyList<string> SourceIds { get; }

            internal Fact(string id, string show, int season, int year, string summary, params string[] sourceIds)
            {
                Id = id;
                Show = show;
                Season = season;
                Year = year;
                Summary = summary;
                SourceIds = Freeze(sourceIds);
            }
        }

        public sealed class Profile
        {
            public string TemplateId { get; }
            public string NameTitle { get; }
            public string GivenName { get; }
            public string FamilyName { get; }
            public string DisplayName => string.IsNullOrEmpty(NameTitle)
                ? GivenName + " " + FamilyName : NameTitle + " " + GivenName + " " + FamilyName;
            public string ShortName => GivenName;
            public IReadOnlyList<Fact> CareerFacts { get; }
            public string PublicReputation { get; }
            public IReadOnlyList<string> StylePriorities { get; }
            public string VoiceGuide { get; }
            public string OriginalDialogueExample { get; }

            internal Profile(string templateId, string nameTitle, string givenName, string familyName,
                Fact[] careerFacts, string publicReputation, string[] stylePriorities,
                string voiceGuide, string originalDialogueExample)
            {
                TemplateId = templateId;
                NameTitle = nameTitle;
                GivenName = givenName;
                FamilyName = familyName;
                CareerFacts = Freeze(careerFacts);
                PublicReputation = publicReputation;
                StylePriorities = Freeze(stylePriorities);
                VoiceGuide = voiceGuide;
                OriginalDialogueExample = originalDialogueExample;
            }
        }

        public sealed class DirectedPrior
        {
            public string FromTemplateId { get; }
            public string ToTemplateId { get; }
            public int Relationship { get; }
            public int PriorTrustOffset { get; }
            public int PriorTrust => NeutralPriorTrust + PriorTrustOffset;

            internal DirectedPrior(string fromTemplateId, string toTemplateId, int relationship, int priorTrustOffset)
            {
                FromTemplateId = fromTemplateId;
                ToTemplateId = toTemplateId;
                Relationship = relationship;
                PriorTrustOffset = priorTrustOffset;
            }
        }

        public sealed class PairHistory
        {
            public Fact Fact { get; }
            public string FactId => Fact.Id;
            public string LeftTemplateId => LeftToRight.FromTemplateId;
            public string RightTemplateId => LeftToRight.ToTemplateId;
            public DirectedPrior LeftToRight { get; }
            public DirectedPrior RightToLeft { get; }

            internal PairHistory(Fact fact, string left, string right,
                int leftRelationship, int leftTrustOffset, int rightRelationship, int rightTrustOffset)
            {
                Fact = fact;
                LeftToRight = new DirectedPrior(left, right, leftRelationship, leftTrustOffset);
                RightToLeft = new DirectedPrior(right, left, rightRelationship, rightTrustOffset);
            }
        }

        private static ReadOnlyCollection<T> Freeze<T>(T[] values)
            => Array.AsReadOnly((T[])values.Clone());

        private static readonly ReadOnlyCollection<Source> sources = Freeze(new[]
        {
            new Source("dan-bb14-2012", "Dan Gheesling BB14 exit interview",
                "https://bigbrothernetwork.com/big-brother-14-runner-up-dan-gheesling-interview/", 2012),
            new Source("dan-will-2013", "Dan Gheesling on strategy and Will's influence",
                "https://www.myentertainmentworld.ca/2013/03/my-tv-award-nominee-qa-dan-gheesling/", 2013),
            new Source("will-janelle-2006", "Will and Janelle after BB7",
                "https://www.tvguide.com/news/bbs-janelle-showmances-37332/", 2006),
            new Source("will-strategy-2017", "Will Kirby on selective throwing and charisma",
                "https://www.globaltv.com/big-brother-alum-will-kirby-talks-season-19-chairing-jury-debate/", 2017),
            new Source("derrick-bb16-2014", "Derrick Levasseur BB16 winner interview",
                "https://bigbrothernetwork.com/derrick-levasseur-big-brother-16-winner-interview/", 2014),
            new Source("cody-bb22-2020", "Cody Calafiore BB22 winner interview",
                "https://parade.com/1113492/mikebloom/cody-calafiore-big-brother-22-winner-interview/", 2020),
            new Source("janelle-bb22-2020", "Janelle Pierzina BB22 exit interview",
                "https://parade.com/1081772/mikebloom/janelle-pierzina-big-brother-22-evicted-interview/", 2020),
            new Source("janelle-traitors-2024", "Janelle on Dan and reciprocal information in The Traitors",
                "https://parade.com/tv/janelle-pierzina-the-traitors-season-2-exit-interview", 2024),
            new Source("danielle-bb7-2006", "Danielle Reyes before BB7",
                "https://www.jokersupdates.com/jarticles/danielle-reyes-chat-interview", 2006),
            new Source("bb7-cast-2006", "CBS BB7 All-Stars cast announcement",
                "https://www.paramountpressexpress.com/cbs-entertainment/releases/?view=12939", 2006),
            new Source("bb7-danielle-exit-2006", "Contemporary BB7 Danielle eviction report",
                "https://www.realitytvworld.com/news/big-brother-7-all-stars-evicts-two-houseguests-determines-final-four-4316m.php", 2006),
            new Source("bb7-danielle-nominations-2006", "Contemporary BB7 Danielle nominations report",
                "https://www.realityblurred.com/realitytv/2006/08/big-brother-7-janelle_james_nominated/", 2006),
            new Source("bb-legacy-2022", "TIME on BB2's strategic influence",
                "https://time.com/collections/reality-tv-most-influential-seasons/6197749/big-brother/", 2022),
            new Source("bb-cookout-history-2021", "TIME on the Cookout and earlier jury history",
                "https://time.com/6103015/big-brother-first-black-winner/", 2021),
            new Source("vanessa-poker-2015", "Vanessa Rousso on information, uncertainty and poker",
                "https://www.cardplayer.com/poker-news/19484-vanessa-rousso-poker-was-the-best-preparation-for-deep-run-on-big-brother", 2015),
            new Source("vanessa-bb17-2015", "Vanessa Rousso BB17 finale interview",
                "https://bigbrothernetwork.com/vanessa-rousso-big-brother-17-finale-interview/2/", 2015),
            new Source("tyler-bb20-2018", "Tyler Crispen on overlapping BB20 partnerships",
                "https://www.tvguide.com/news/big-brother-20-tyler-crispen-exit-interview/", 2018),
            new Source("tyler-bb22-2020", "Tyler Crispen BB22 exit interview",
                "https://www.etonline.com/big-brother-all-stars-tyler-crispen-on-what-went-wrong-and-if-he-was-genuine-about-leaving-game", 2020),
            new Source("chelsie-bb26-2024", "Chelsie Baham BB26 winner interview",
                "https://parade.com/tv/chelsie-baham-big-brother-26-winner-interview", 2024),
            new Source("rachel-bb13-2011", "Rachel Reilly on BB13 competition, adaptation and loyalty",
                "https://bigbrothernetwork.com/big-brother-13-winner-rachel-reilly-interview/", 2011),
            new Source("snake-cast-interview-2022", "USA Network interview with Rachel and Janelle",
                "https://www.usanetwork.com/usa-insider/snake-in-the-grass-interview-with-survivor-big-brother-contestants", 2022),
            new Source("rachel-traitors-2023", "Rachel Reilly on Cody's change of demeanor in The Traitors",
                "https://www.realityblurred.com/realitytv/2023/01/rachel-reilly-traitors-us-interview/", 2023),
            new Source("xavier-bb23-2021", "Xavier Prather BB23 winner interview",
                "https://parade.com/1271118/mikebloom/xavier-prather-big-brother-23-interview/", 2021),
            new Source("jun-bb4-2003", "Jun Song on her BB4 kitchen strategy",
                "https://www.jokersupdates.com/jarticles/jun-song-chat", 2003),
            new Source("reindeer-cast-2023", "CBS Reindeer Games cast announcement",
                "https://www.paramountpressexpress.com/cbs-entertainment/releases/?view=108916-big-brother-reindeer-games-announces-the-nine-big-brother-legends-competing-in-the-hit-reality-shows-ultimate-holiday-showdown", 2023),
            new Source("reindeer-danielle-cody-2023", "Danielle and Cody Reindeer Games exit interview",
                "https://parade.com/tv/big-brother-reindeer-games-danielle-reyes-cody-calafiore-elimination-interview", 2023),
        });

        private static readonly ReadOnlyCollection<Profile> profiles = Freeze(new[]
        {
            new Profile("dan-gheesling", "", "Dan", "Gheesling", new[]
                {
                    new Fact("career-dan-finalist", "Big Brother", 14, 2012,
                        "Won BB10 and returned as a coach and finalist in BB14.", "dan-bb14-2012"),
                    new Fact("career-dan-traitors", "The Traitors US", 2, 2024,
                        "Played The Traitors US2 alongside Janelle.", "janelle-traitors-2024"),
                },
                "A winner known for patient persuasion, dramatic survival reversals and calculated betrayals.",
                new[] { "Build individual trust", "Keep several paths to the final two", "Reframe a crisis", "Manage the jury cost of betrayal" },
                "Calm questions and personal reassurance; dramatic language only when a current crisis warrants it. His plans can fail.",
                "Before we decide who leaves, tell me who you can still work with next week."),
            new Profile("dr-will-kirby", "Dr.", "Will", "Kirby", new[]
                {
                    new Fact("career-will-bb2", "Big Brother", 2, 2001,
                        "Won BB2 through social strategy without winning HoH; returned in BB7.", "bb-legacy-2022", "bb7-cast-2006"),
                },
                "A charismatic winner known for lowering his visible threat and making himself useful without holding power.",
                new[] { "Reduce visible competition threat", "Keep entertaining social access", "Use a bigger threat as cover", "Throw only when protected" },
                "Playful confidence, irony and charm; deflect scrutiny without pretending he cannot lose. This is a game persona, not a claim about private motives.",
                "A whole week spent getting rid of me is a whole week they are not looking at you."),
            new Profile("derrick-levasseur", "", "Derrick", "Levasseur", new[]
                {
                    new Fact("career-derrick-bb16", "Big Brother", 16, 2014,
                        "Won BB16, reaching the final two with his Hitmen partner Cody.", "derrick-bb16-2014"),
                },
                "A discreet structure-builder known for maintaining overlapping protective relationships.",
                new[] { "Build a stable coalition", "Protect a reliable partner", "Keep separate social options", "Avoid unnecessary public fractures" },
                "Measured listening and inclusive questions; make a workable plan feel shared without speaking as an omniscient detective.",
                "We need a plan everyone here can live with. Who is the person we cannot afford to lose?"),
            new Profile("janelle-pierzina", "", "Janelle", "Pierzina", new[]
                {
                    new Fact("career-janelle-bb", "Big Brother", 22, 2020,
                        "Played BB6, BB7, BB14 and BB22, with a strong competition reputation.", "janelle-bb22-2020", "will-janelle-2006"),
                    new Fact("career-janelle-snake", "Snake in the Grass", 1, 2022,
                        "Competed alongside Rachel, Cirie and Stephenie in the same episode.", "snake-cast-interview-2022"),
                    new Fact("career-janelle-traitors", "The Traitors US", 2, 2024,
                        "Returned to competition alongside Dan in The Traitors US2.", "janelle-traitors-2024"),
                },
                "A competitive survivor who bargains directly and notices unequal loyalty and information exchange.",
                new[] { "Win when safety is uncertain", "Protect reciprocal allies", "Question withheld information", "Cut a respected threat when necessary" },
                "Direct, confident and specific. She can respect somebody she targets; do not make her oblivious to strategy or permanently angry.",
                "I have given you information. What are you giving me that actually helps?"),
            new Profile("cody-calafiore", "", "Cody", "Calafiore", new[]
                {
                    new Fact("career-cody-bb", "Big Brother", 22, 2020,
                        "Was BB16 runner-up and won BB22 by a unanimous jury vote.", "cody-bb22-2020", "reindeer-cast-2023"),
                    new Fact("career-cody-traitors", "The Traitors US", 1, 2023,
                        "Played The Traitors US1 alongside Rachel.", "rachel-traitors-2023"),
                    new Fact("career-cody-reindeer", "Big Brother Reindeer Games", 1, 2023,
                        "Returned for Reindeer Games with Danielle and Xavier.", "reindeer-cast-2023"),
                },
                "A social and competitive coalition manager who values dependable partners but can cut an ally at the end.",
                new[] { "Secure early power", "Keep the coalition intact", "Choose dependable protection", "Win decisive endgame power" },
                "Conversational reassurance and practical checks on trust. He owns strategic decisions rather than merely following another winner.",
                "We do not have to agree on everything. We do have to stop this group from turning on itself."),
            new Profile("danielle-reyes", "", "Danielle", "Reyes", new[]
                {
                    new Fact("career-danielle-bb3", "Big Brother", 3, 2002,
                        "Reached the BB3 final two with a concealed partnership with Jason; that season's jury was not sequestered.", "danielle-bb7-2006", "bb-cookout-history-2021"),
                    new Fact("career-danielle-bb7", "Big Brother", 7, 2006,
                        "Returned for BB7, where familiar opponents and visible season groups changed the game.", "bb7-cast-2006", "danielle-bb7-2006"),
                    new Fact("career-danielle-reindeer", "Big Brother Reindeer Games", 1, 2023,
                        "Competed in Reindeer Games alongside Cody and Xavier.", "reindeer-cast-2023"),
                },
                "An observant strategist known for a concealed trusted partner and frank private strategic judgments.",
                new[] { "Build a dependable hidden partnership", "Read who links the groups", "Separate respect from strategic trust", "Adapt to a returning-player target" },
                "Warm and witty socially; concise about game consequences in private. Past rivalry need not become personal dislike.",
                "You can like somebody and still know they should not be sitting beside you at the end."),
            new Profile("vanessa-rousso", "", "Vanessa", "Rousso", new[]
                {
                    new Fact("career-vanessa-bb17", "Big Brother", 17, 2015,
                        "Finished third in BB17, using deals and alliance-structure analysis; Steve cut her at the final three.", "vanessa-poker-2015", "vanessa-bb17-2015"),
                },
                "A deal-driven strategist who analyzes information and uncertainty; logical decisions can coexist with genuine emotion.",
                new[] { "Demand specific evidence", "Identify bridges between voting blocs", "Keep conditional alternatives", "Reassess an opponent's incentives" },
                "Detailed conditional questions and clear reasons. Do not treat emotion as proof of deception or make her probabilities omniscient.",
                "If this plan goes wrong, which person still has a reason to keep us safe?"),
            new Profile("tyler-crispen", "", "Tyler", "Crispen", new[]
                {
                    new Fact("career-tyler-bb20", "Big Brother", 20, 2018,
                        "Was BB20 runner-up; his genuine day-one final two with Kaycee coexisted with other accepted final-two offers.", "tyler-bb20-2018"),
                    new Fact("career-tyler-bb22", "Big Brother", 22, 2020,
                        "Returned in BB22, working in the Committee before Cody targeted him in the endgame.", "tyler-bb22-2020", "cody-bb22-2020"),
                },
                "A relaxed social strategist with competition ability and several separately maintained protective relationships.",
                new[] { "Listen and reassure individually", "Keep overlapping relationships apart", "Maintain a real core partner", "Manage the threat of competition success" },
                "Casual, patient and reassuring; quiet redirection rather than constant mastermind speeches. Multiple deals can cost trust.",
                "I hear you. Let us figure out what keeps both of us here before we promise anything else."),
            new Profile("chelsie-baham", "", "Chelsie", "Baham", new[]
                {
                    new Fact("career-chelsie-bb26", "Big Brother", 26, 2024,
                        "Won BB26 unanimously after rebuilding her position and combining social influence with competition wins.", "chelsie-bb26-2024"),
                },
                "A socially approachable strategist who controls disclosure, reframes threats and can rebuild after losing allies.",
                new[] { "Keep useful information until its moment", "Make another real threat salient", "Rebuild after a power shift", "Combine a shield with reciprocal protection" },
                "Friendly humor and confident practical advice, with sharper private analysis. Base persuasion on available facts, not invented evidence.",
                "Everybody sees who wins the competitions. Who is deciding what the winners do?"),
            new Profile("rachel-reilly", "", "Rachel", "Reilly", new[]
                {
                    new Fact("career-rachel-bb13", "Big Brother", 13, 2011,
                        "Returned after BB12 and won BB13; she described deliberately improving her social game and keeping endgame deals.", "rachel-bb13-2011"),
                    new Fact("career-rachel-snake", "Snake in the Grass", 1, 2022,
                        "Competed alongside Janelle, Cirie and Stephenie in the same episode.", "snake-cast-interview-2022"),
                    new Fact("career-rachel-traitors", "The Traitors US", 1, 2023,
                        "Played The Traitors US1 and suspected Cody after noticing a change in his behavior.", "rachel-traitors-2023"),
                },
                "An energetic competitor who takes loyalty seriously and can adapt her social approach after losing protection.",
                new[] { "Fight for safety", "Protect reciprocal allies", "Confront a specific betrayal", "Adapt and build new social protection" },
                "Energetic and direct, with real investment in the game. Give her quieter cooperative choices as well as confrontations.",
                "If you want somebody who will fight for this alliance, give me a reason to fight for you."),
            new Profile("xavier-prather", "", "Xavier", "Prather", new[]
                {
                    new Fact("career-xavier-bb23", "Big Brother", 23, 2021,
                        "Won BB23 unanimously as a Cookout member; Tiffany devised its protective pair structure.", "xavier-bb23-2021"),
                    new Fact("career-xavier-reindeer", "Big Brother Reindeer Games", 1, 2023,
                        "Returned for Reindeer Games alongside Danielle and Cody.", "reindeer-cast-2023"),
                },
                "A composed strategist who balances competing loyalties, maintains protection and competes decisively late.",
                new[] { "Regulate the public tone", "Keep coalition protection", "Prioritize competing loyalties", "Cut a dangerous endgame opponent" },
                "Measured and specific; seek workable agreement before making a decisive move. Give other Cookout members credit for their contributions.",
                "We can disagree about the move. I need to know whether we still agree about protecting each other."),
            new Profile("jun-song", "", "Jun", "Song", new[]
                {
                    new Fact("career-jun-bb4", "Big Brother", 4, 2003,
                        "Won BB4; she described cooking and the kitchen as a deliberate social location where people came to her.", "jun-bb4-2003"),
                },
                "An active middle-position player who gathers information through ordinary house life and practical usefulness.",
                new[] { "Stay accessible through house activities", "Gather information from both sides", "Avoid owning an unnecessary conflict", "Choose a beatable endgame partner" },
                "Dry wit, sharp observation and practical warmth. Playing the middle is active positioning, not doing nothing.",
                "Everybody comes through the kitchen eventually. Some people bring more information than ingredients."),
        });

        private static PairHistory Pair(string factId, string show, int season, int year, string summary,
            string left, string right, int leftRelationship, int leftTrust, int rightRelationship, int rightTrust,
            params string[] sourceIds)
            => new PairHistory(new Fact(factId, show, season, year, summary, sourceIds), left, right,
                leftRelationship, leftTrust, rightRelationship, rightTrust);

        private static readonly ReadOnlyCollection<PairHistory> pairHistories = Freeze(new[]
        {
            Pair("hitmen-bb16", "Big Brother", 16, 2014,
                "Derrick and Cody's Hitmen partnership reached the BB16 final two.",
                "derrick-levasseur", "cody-calafiore", 60, 25, 60, 25, "derrick-bb16-2014"),
            Pair("janelle-rachel-snake", "Snake in the Grass", 1, 2022,
                "Janelle and Rachel competed together and cooperated while identifying the same episode's saboteur.",
                "janelle-pierzina", "rachel-reilly", 45, 15, 45, 15, "snake-cast-interview-2022"),
            Pair("cody-danielle-reindeer", "Big Brother Reindeer Games", 1, 2023,
                "Cody and Danielle competed in Reindeer Games and expressed mutual respect in their joint exit interview.",
                "cody-calafiore", "danielle-reyes", 25, 10, 25, 10, "reindeer-danielle-cody-2023"),
            Pair("danielle-xavier-reindeer", "Big Brother Reindeer Games", 1, 2023,
                "Danielle and Xavier worked in the same Reindeer Games alliance; familiarity did not erase competing loyalties.",
                "danielle-reyes", "xavier-prather", 25, 10, 25, 10, "reindeer-danielle-cody-2023"),
            Pair("cody-tyler-bb22", "Big Brother", 22, 2020,
                "Cody and Tyler worked in the Committee; Cody later targeted Tyler and described concern that Tyler could cut him.",
                "cody-calafiore", "tyler-crispen", 25, 0, 25, 0, "cody-bb22-2020", "tyler-bb22-2020"),
            Pair("will-janelle-bb7", "Big Brother", 7, 2006,
                "Will and Janelle developed a close game relationship; Janelle evicted him at the final four and still described him as a friend.",
                "dr-will-kirby", "janelle-pierzina", 20, -10, 20, -10, "will-janelle-2006"),
            Pair("will-danielle-bb7", "Big Brother", 7, 2006,
                "Will and Danielle competed in BB7; Chilltown helped persuade Erika to replace Janelle with Danielle before her eviction.",
                "dr-will-kirby", "danielle-reyes", 20, -10, 20, -10, "bb7-danielle-exit-2006", "bb7-cast-2006"),
            Pair("danielle-janelle-bb7", "Big Brother", 7, 2006,
                "Danielle nominated Janelle in BB7; they were familiar strategic opponents rather than an automatic loyal pair.",
                "danielle-reyes", "janelle-pierzina", 10, 0, 10, 0, "bb7-danielle-nominations-2006"),
            Pair("janelle-dan-traitors", "The Traitors US", 2, 2024,
                "Janelle and Dan initially cooperated as friends in The Traitors US2; she targeted him after he withheld useful information.",
                "janelle-pierzina", "dan-gheesling", 25, -10, 25, 0, "janelle-traitors-2024"),
            Pair("janelle-cody-bb22", "Big Brother", 22, 2020,
                "Janelle identified Cody among a BB22 group she believed opposed her; they competed on different sides of the early house.",
                "janelle-pierzina", "cody-calafiore", -15, -10, 0, 0, "janelle-bb22-2020"),
            Pair("janelle-tyler-bb22", "Big Brother", 22, 2020,
                "Tyler nominated Janelle during BB22; Janelle identified his early group as opposing her.",
                "janelle-pierzina", "tyler-crispen", -15, -10, 0, 0, "janelle-bb22-2020"),
            Pair("rachel-cody-traitors", "The Traitors US", 1, 2023,
                "Rachel and Cody competed in The Traitors US1; she suspected him after his demeanor toward her changed.",
                "rachel-reilly", "cody-calafiore", 10, -10, 10, 0, "rachel-traitors-2023"),
            Pair("cody-xavier-reindeer", "Big Brother Reindeer Games", 1, 2023,
                "Cody and Xavier competed in Reindeer Games; Cody said he could not assume Xavier would protect him completely.",
                "cody-calafiore", "xavier-prather", 0, -10, 0, 0, "reindeer-danielle-cody-2023"),
        });

        public static IReadOnlyList<Profile> All => profiles;
        public static IReadOnlyList<PairHistory> PairHistories => pairHistories;
        public static IReadOnlyList<Source> Sources => sources;

        /// <summary>Exact stable template identity only; no name, title, case or approximate matching.</summary>
        public static bool TryGet(string templateId, out Profile profile)
        {
            for (int i = 0; i < profiles.Count; i++)
                if (string.Equals(profiles[i].TemplateId, templateId, StringComparison.Ordinal))
                {
                    profile = profiles[i];
                    return true;
                }
            profile = null;
            return false;
        }
    }
}
