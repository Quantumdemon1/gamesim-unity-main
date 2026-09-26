using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>The first-night introductions' content and scoring, as the reference build has them.</summary>
    public sealed class WebIntroductionsTests
    {
        [Test]
        public void ThereAreThreeWaysToIntroduceYourselfInTheReferenceOrder()
        {
            Assert.That(WebIntroductions.Approaches.Select(approach => approach.Label), Is.EqualTo(new[] { "Warm", "Calculated", "Bold" }));
            Assert.That(WebIntroductions.Approaches.Select(approach => approach.Id), Is.EqualTo(new[] { "warm", "calculated", "bold" }));
            Assert.That(WebIntroductions.Approaches.Select(approach => approach.Line), Is.EqualTo(new[]
            {
                "\"Great to meet you! I love your energy.\"",
                "\"You seem like someone worth knowing.\"",
                "\"I'm going to win this thing. You in?\"",
            }));
            Assert.That(WebIntroductions.Prompt, Is.EqualTo("How do you introduce yourself?"));
            Assert.That(WebIntroductions.Find("bold").Label, Is.EqualTo("Bold"));
            Assert.That(WebIntroductions.Find("shy"), Is.Null);
        }

        /// <summary>
        /// Every trait counts, not just the first: an approach lands when something about them likes
        /// it and nothing about them minds it, falls flat when all they have is something that minds
        /// it, and is merely polite otherwise.
        /// </summary>
        [TestCase("warm", new[] { "Social", "Sneaky" }, WebIntroductions.Outcome.Match)]
        [TestCase("warm", new[] { "Strategic", "Social" }, WebIntroductions.Outcome.Neutral)]
        [TestCase("warm", new[] { "Analytical", "Strategic" }, WebIntroductions.Outcome.Clash)]
        [TestCase("calculated", new[] { "Analytical", "Sneaky" }, WebIntroductions.Outcome.Match)]
        [TestCase("calculated", new[] { "Confrontational", "Social" }, WebIntroductions.Outcome.Clash)]
        [TestCase("calculated", new[] { "Emotional", "Strategic" }, WebIntroductions.Outcome.Neutral)]
        [TestCase("bold", new[] { "Competitive", "Confrontational" }, WebIntroductions.Outcome.Match)]
        [TestCase("bold", new[] { "Introverted", "Loyal" }, WebIntroductions.Outcome.Clash)]
        [TestCase("bold", new[] { "Charming", "Intuitive" }, WebIntroductions.Outcome.Neutral)]
        [TestCase("warm", new string[0], WebIntroductions.Outcome.Neutral)]
        public void AnApproachLandsByTheTraitsItMeets(string approach, string[] traits, WebIntroductions.Outcome expected)
        {
            Assert.That(WebIntroductions.Judge(WebIntroductions.Find(approach), traits), Is.EqualTo(expected));
        }

        [Test]
        public void AGoodFirstImpressionIsWorthThreeABadOneCostsThreeAndAPoliteOneOne()
        {
            Assert.That(WebIntroductions.Bonus(WebIntroductions.Outcome.Match), Is.EqualTo(3));
            Assert.That(WebIntroductions.Bonus(WebIntroductions.Outcome.Neutral), Is.EqualTo(1));
            Assert.That(WebIntroductions.Bonus(WebIntroductions.Outcome.Clash), Is.EqualTo(-3));
        }

        [Test]
        public void AnAllStarSaysTheirOwnLineAndAnyoneElseSpeaksToTheirFirstTrait()
        {
            string dan = WebIntroductions.IntroLine(7, "dan-gheesling", "Dan Gheesling", new[] { "Strategic" });
            Assert.That(new[]
            {
                "Welcome to the mist. By the time you figure out my game, it'll be too late.",
                "I coach football — reading people and calling plays is what I do.",
                "Dan's Funeral was just the beginning. I've got a whole new playbook.",
            }, Does.Contain(dan));

            string strategist = WebIntroductions.IntroLine(7, "alex-chen", "Alex Chen", new[] { "Strategic", "Social" });
            Assert.That(new[] { "I'm not here to make friends… well, maybe a few.", "Every conversation is a chess move. Let's play." }, Does.Contain(strategist));

            string nobody = WebIntroductions.IntroLine(7, "you", "You", new string[0]);
            Assert.That(new[] { "Hey everyone, excited to be here! Let's have a great time.", "Ready to make some moves and meet some amazing people!" }, Does.Contain(nobody));
        }

        [Test]
        public void AnAnswerComesFromTheirFirstTraitAndOutcomeOrFromTheSociablePoolsWhenTheTraitHasNone()
        {
            string clash = WebIntroductions.Reaction(3, "taylor-kim", "Taylor Kim", new[] { "Competitive", "Confrontational" }, WebIntroductions.Outcome.Clash);
            Assert.That(new[] { "You don't seem like much competition.", "Hmm. Alright." }, Does.Contain(clash));

            string unknown = WebIntroductions.Reaction(3, "x", "Somebody", new[] { "Mysterious" }, WebIntroductions.Outcome.Match);
            Assert.That(new[] { "Oh my gosh, I love you already!", "This is going to be SO fun!" }, Does.Contain(unknown));

            string rachel = WebIntroductions.Reaction(3, "rachel-reilly", "Rachel Reilly", new[] { "Emotional" }, WebIntroductions.Outcome.Clash);
            Assert.That(new[] { "*dramatic gasp* HOW DARE YOU!", "You do NOT want me as your enemy. Trust. Me." }, Does.Contain(rachel));
        }

        /// <summary>
        /// A line is the same every time the same person is met in the same season - reloads
        /// included - and different people in a house do not all say the same thing.
        /// </summary>
        [Test]
        public void TheLinePickedIsFixedBySeasonAndPersonAndVariesAcrossAHouse()
        {
            var traits = new[] { "Social" };
            Assert.That(WebIntroductions.IntroLine(11, "casey-wilson", "Casey Wilson", traits),
                Is.EqualTo(WebIntroductions.IntroLine(11, "casey-wilson", "Casey Wilson", traits)));
            var lines = new HashSet<string>(Enumerable.Range(0, 12).Select(i => WebIntroductions.IntroLine(11, "guest-" + i, "Guest " + i, traits)));
            Assert.That(lines, Has.Count.EqualTo(2), "Both of the pool's lines turn up across a house of twelve.");
            var seasons = new HashSet<string>(Enumerable.Range(0, 12).Select(seed => WebIntroductions.IntroLine(seed, "casey-wilson", "Casey Wilson", traits)));
            Assert.That(seasons, Has.Count.EqualTo(2), "The same person does not say the same thing in every season.");
        }

        /// <summary>
        /// The share of an impression a houseguest hands back is fixed the way a line is: the same
        /// for the same person in the same season, always inside the reference build's roll, and not
        /// the same for everybody in a house or for one person in every season.
        /// </summary>
        [Test]
        public void TheReciprocalIsFixedBySeasonAndPersonAndStaysInTheWebsBand()
        {
            Assert.That(WebIntroductions.ReciprocalRoll(5, "alex-chen"), Is.EqualTo(WebIntroductions.ReciprocalRoll(5, "alex-chen")));

            var house = Enumerable.Range(0, 12).Select(i => WebIntroductions.ReciprocalRoll(5, "guest-" + i)).ToList();
            var seasons = Enumerable.Range(0, 12).Select(seed => WebIntroductions.ReciprocalRoll(seed, "alex-chen"))
                .Concat(new[] { int.MinValue, -1, int.MaxValue }.Select(seed => WebIntroductions.ReciprocalRoll(seed, "alex-chen")))
                .ToList();
            foreach (double roll in house.Concat(seasons))
                Assert.That(roll, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));

            Assert.That(house.Distinct().Count(), Is.EqualTo(house.Count), "Nobody in a house of twelve hands back exactly what another does.");
            Assert.That(house.Max() - house.Min(), Is.GreaterThan(0.5), "Across a house the rolls span the band rather than bunching.");
            Assert.That(seasons.Distinct().Count(), Is.EqualTo(seasons.Count), "The same person does not hand back the same share in every season.");
            Assert.That(WebIntroductions.ReciprocalRoll(5, "alex-chen"), Is.Not.EqualTo(WebIntroductions.ReciprocalRoll(5, "emma-brown")));
        }

        /// <summary>
        /// An answer is spoken by whichever trait decided it, not by whichever trait is listed first.
        /// The reference build voiced every answer from the first trait, so a warm hello to Quinn -
        /// Confrontational, then Social, and a match only because of the second - could be answered
        /// "Bring it on!".
        /// </summary>
        [Test]
        public void AnAnswerSpeaksInTheVoiceOfTheTraitThatDecidedIt()
        {
            var warm = WebIntroductions.Find(WebIntroductions.Warm);
            var calculated = WebIntroductions.Find(WebIntroductions.Calculated);
            var bold = WebIntroductions.Find(WebIntroductions.Bold);
            var jordan = new[] { "Social", "Sneaky" };
            var quinn = new[] { "Confrontational", "Social" };
            var jamie = new[] { "Emotional", "Strategic" };

            // Jordan is Social first, but it is being Sneaky that a calculated hello lands with.
            Assert.That(WebIntroductions.DecidingTrait(calculated, jordan), Is.EqualTo("Sneaky"));
            Assert.That(new[] { "Smart. You know when to be subtle.", "I like how you operate." },
                Does.Contain(WebIntroductions.Reaction(5, "jordan-taylor", "Jordan Taylor", jordan, calculated)));

            Assert.That(WebIntroductions.DecidingTrait(warm, quinn), Is.EqualTo("Social"));
            Assert.That(new[] { "Oh my gosh, I love you already!", "This is going to be SO fun!" },
                Does.Contain(WebIntroductions.Reaction(5, "quinn-martinez", "Quinn Martinez", quinn, warm)));

            Assert.That(WebIntroductions.DecidingTrait(calculated, quinn), Is.EqualTo("Confrontational"));
            Assert.That(new[] { "Ugh, spare me the niceties.", "Be real with me." },
                Does.Contain(WebIntroductions.Reaction(5, "quinn-martinez", "Quinn Martinez", quinn, calculated)));

            // Nothing in particular decides a neutral answer, so it comes from the first trait.
            Assert.That(WebIntroductions.DecidingTrait(bold, jamie), Is.EqualTo("Emotional"));
            Assert.That(new[] { "That's cool. I vibe with that.", "Nice energy." },
                Does.Contain(WebIntroductions.Reaction(5, "jamie-roberts", "Jamie Roberts", jamie, bold)));

            // Somebody with no traits, or none with lines of their own, answers in the sociable voice.
            Assert.That(WebIntroductions.DecidingTrait(warm, new string[0]), Is.Null);
            Assert.That(new[] { "Cool cool! Welcome to the house!", "Hey, nice!" },
                Does.Contain(WebIntroductions.Reaction(5, "x", "Somebody", new string[0], warm)));
            Assert.That(new[] { "Cool cool! Welcome to the house!", "Hey, nice!" },
                Does.Contain(WebIntroductions.Reaction(5, "x", "Somebody", new[] { "Mysterious" }, warm)));

            // An All-Star still answers in their own words, as they did before.
            string rachel = WebIntroductions.Reaction(3, "rachel-reilly", "Rachel Reilly", new[] { "Emotional" }, warm);
            Assert.That(rachel, Is.EqualTo(WebIntroductions.Reaction(3, "rachel-reilly", "Rachel Reilly", new[] { "Emotional" }, WebIntroductions.Outcome.Match)));
            Assert.That(new[] { "OH MY GOD, I love you already! *squeals*", "We're going to be BEST friends! No one can stop us!" }, Does.Contain(rachel));
        }

        /// <summary>Every line can be drawn: nothing empty, and nothing the house's type has no glyph for.</summary>
        [Test]
        public void EveryLineIsTextTheHouseCanDraw()
        {
            var every = new List<string>();
            foreach (var approach in WebIntroductions.Approaches) every.Add(approach.Line);
            var outcomes = new[] { WebIntroductions.Outcome.Match, WebIntroductions.Outcome.Neutral, WebIntroductions.Outcome.Clash };
            var traits = new[] { "Strategic", "Competitive", "Loyal", "Social", "Charming", "Manipulative", "Analytical", "Emotional",
                "Funny", "Deceptive", "Introverted", "Stubborn", "Flexible", "Intuitive", "Sneaky", "Confrontational", "Impulsive", "None" };
            var names = new[] { "Dan Gheesling", "Dr. Will Kirby", "Janelle Pierzina", "Rachel Reilly", "Derrick Levasseur", "Tyler Crispen",
                "Vanessa Rousso", "Cody Calafiore", "Danielle Reyes", "Xavier Prather", "Jun Song", "Chelsie Baham", "Nobody" };
            for (int seed = 0; seed < 8; seed++)
                foreach (var trait in traits)
                    foreach (var name in names)
                    {
                        every.Add(WebIntroductions.IntroLine(seed, name, name, new[] { trait }));
                        foreach (var outcome in outcomes) every.Add(WebIntroductions.Reaction(seed, name, name, new[] { trait }, outcome));
                    }
            Assert.That(every.Where(string.IsNullOrWhiteSpace), Is.Empty);
            var undrawable = every.Where(line => line.Any(c => c > 0x2100 || char.IsSurrogate(c))).Distinct().ToList();
            Assert.That(undrawable, Is.Empty, "Lines with characters outside the house's type: " + string.Join(" | ", undrawable));
        }
    }
}
