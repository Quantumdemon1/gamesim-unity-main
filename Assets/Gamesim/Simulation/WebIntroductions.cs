using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The first-night introductions, as the reference build wrote them (its MeetAndGreetPhase).
    /// Each houseguest says a line of their own, the player introduces themselves Warm, Calculated
    /// or Bold, and how that lands is read off the houseguest's traits: an approach they have an
    /// affinity for and no clash with makes a good first impression (+3), one they only clash with
    /// makes a bad one (-3), and anything else a neutral one (+1). Nothing is rolled; reading the
    /// traits is the skill.
    ///
    /// <para>The lines are the reference build's own, word for word, with one exception: a reaction
    /// that ended in an emoji ends in "*eye roll*", in the style of its other stage directions, because
    /// the house's type has no glyph for it.</para>
    ///
    /// <para>The reference build picks a line at random each time the phase mounts. Here a line is
    /// picked from the season's seed and the houseguest's id instead - the same line every time the
    /// same houseguest is met in the same season, reloads included - and nothing is drawn from the
    /// season's generator, so which lines are shown can never change a result. The share of the
    /// impression a houseguest hands back, which the reference build also rolled, is fixed the same
    /// way (<see cref="ReciprocalRoll"/>).</para>
    ///
    /// <para>One reaction is voiced differently on purpose. The reference build answered from the
    /// houseguest's first trait whichever trait had decided the outcome, so a warm hello to somebody
    /// Confrontational and Social - a match, because they are Social - could be answered "Bring it
    /// on!". Here the answer comes in the voice of the trait that decided it
    /// (<see cref="DecidingTrait"/>); the reference build's voicing is kept as the overload of
    /// <see cref="Reaction(int, string, string, IList{string}, Outcome)"/> that takes an outcome.</para>
    /// </summary>
    public static class WebIntroductions
    {
        public enum Outcome { Match, Neutral, Clash }

        public sealed class Approach
        {
            /// <summary>What the command carries: "warm", "calculated" or "bold".</summary>
            public readonly string Id;
            /// <summary>The choice's caption.</summary>
            public readonly string Label;
            /// <summary>What the player says, quote marks included, as the reference build shows it.</summary>
            public readonly string Line;
            public readonly string[] Affinity, Clash;

            public Approach(string id, string label, string line, string[] affinity, string[] clash)
            {
                Id = id; Label = label; Line = line; Affinity = affinity; Clash = clash;
            }
        }

        public const string Warm = "warm", Calculated = "calculated", Bold = "bold";

        /// <summary>The three ways to introduce yourself, in the order they are offered.</summary>
        public static readonly IReadOnlyList<Approach> Approaches = new[]
        {
            new Approach(Warm, "Warm", "\"Great to meet you! I love your energy.\"",
                new[] { "Social", "Emotional", "Loyal", "Funny" },
                new[] { "Strategic", "Manipulative", "Analytical" }),
            new Approach(Calculated, "Calculated", "\"You seem like someone worth knowing.\"",
                new[] { "Strategic", "Analytical", "Manipulative", "Sneaky" },
                new[] { "Emotional", "Loyal", "Confrontational" }),
            new Approach(Bold, "Bold", "\"I'm going to win this thing. You in?\"",
                new[] { "Competitive", "Confrontational", "Stubborn" },
                new[] { "Introverted", "Flexible", "Deceptive" }),
        };

        /// <summary>The prompt over the three choices.</summary>
        public const string Prompt = "How do you introduce yourself?";

        public static Approach Find(string id) =>
            Approaches.FirstOrDefault(approach => string.Equals(approach.Id, id, StringComparison.Ordinal));

        /// <summary>How an approach lands with someone of these traits - all of them, not just the first.</summary>
        public static Outcome Judge(Approach approach, IEnumerable<string> traits)
        {
            if (approach == null) throw new ArgumentNullException(nameof(approach));
            var held = new HashSet<string>(traits ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            bool affinity = approach.Affinity.Any(held.Contains);
            bool clash = approach.Clash.Any(held.Contains);
            if (affinity && !clash) return Outcome.Match;
            if (clash && !affinity) return Outcome.Clash;
            return Outcome.Neutral;
        }

        /// <summary>What a first impression is worth: +3 for a match, -3 for a clash, +1 otherwise.</summary>
        public static int Bonus(Outcome outcome) =>
            outcome == Outcome.Match ? 3 : outcome == Outcome.Clash ? -3 : 1;

        public static string Word(Outcome outcome) =>
            outcome == Outcome.Match ? "match" : outcome == Outcome.Clash ? "clash" : "neutral";

        /// <summary>The houseguest's own line: an All-Star's by name, otherwise by their first trait.</summary>
        public static string IntroLine(int seed, string id, string name, IList<string> traits)
        {
            if (!string.IsNullOrEmpty(name) && AllStarIntro.TryGetValue(name, out var own)) return Pick(own, seed, id, "intro");
            string lead = traits != null && traits.Count > 0 ? traits[0] : null;
            return Pick(lead != null && Intro.TryGetValue(lead, out var pool) ? pool : DefaultIntro, seed, id, "intro");
        }

        /// <summary>
        /// How they answer, as the reference build voiced it: an All-Star's by name, otherwise by
        /// first trait and outcome, Social's when the trait has none. Kept for the reference build's
        /// behaviour; the meet-and-greet answers through the overload that takes the approach.
        /// </summary>
        public static string Reaction(int seed, string id, string name, IList<string> traits, Outcome outcome)
        {
            if (!string.IsNullOrEmpty(name) && AllStarReactions.TryGetValue(name, out var own))
                return Pick(own[(int)outcome], seed, id, "reaction");
            string lead = traits != null && traits.Count > 0 ? traits[0] : "Social";
            if (!Reactions.TryGetValue(lead, out var pools)) pools = Reactions["Social"];
            return Pick(pools[(int)outcome], seed, id, "reaction");
        }

        /// <summary>
        /// How they answer <paramref name="approach"/>: an All-Star's by name, otherwise in the voice
        /// of the trait that decided the outcome (<see cref="DecidingTrait"/>), Social's when that
        /// trait has no lines of its own.
        /// </summary>
        public static string Reaction(int seed, string id, string name, IList<string> traits, Approach approach)
        {
            var outcome = Judge(approach, traits);
            if (!string.IsNullOrEmpty(name) && AllStarReactions.TryGetValue(name, out var own))
                return Pick(own[(int)outcome], seed, id, "reaction");
            string voice = DecidingTrait(approach, traits) ?? "Social";
            if (!Reactions.TryGetValue(voice, out var pools)) pools = Reactions["Social"];
            return Pick(pools[(int)outcome], seed, id, "reaction");
        }

        /// <summary>
        /// Which of their traits decided how <paramref name="approach"/> landed: for a match, the
        /// first of theirs the approach has an affinity for; for a clash, the first of theirs it
        /// clashes with; for a neutral outcome, which nothing in particular decided, their first
        /// trait. Null for somebody with no traits at all.
        /// </summary>
        public static string DecidingTrait(Approach approach, IList<string> traits)
        {
            if (approach == null) throw new ArgumentNullException(nameof(approach));
            if (traits == null || traits.Count == 0) return null;
            switch (Judge(approach, traits))
            {
                case Outcome.Match: return traits.First(trait => approach.Affinity.Contains(trait));
                case Outcome.Clash: return traits.First(trait => approach.Clash.Contains(trait));
                default: return traits[0];
            }
        }

        /// <summary>
        /// How much of a first impression a houseguest hands back, in [0, 1): the roll the reciprocal
        /// score is scaled by. The reference build drew it at random; here it is fixed by the season's
        /// seed and the houseguest's id, like the lines, so an introduction draws nothing from the
        /// season's generator and comes out the same every time the same season is replayed.
        /// </summary>
        public static double ReciprocalRoll(int seed, string id) => (Hash(seed, id, "reciprocal") >> 8) / 16777216.0;

        /// <summary>One of <paramref name="pool"/>, the same for the same seed, houseguest and purpose.</summary>
        public static string Pick(IReadOnlyList<string> pool, int seed, string id, string salt)
        {
            if (pool == null || pool.Count == 0) return string.Empty;
            return pool[(int)(Hash(seed, id, salt) % (uint)pool.Count)];
        }

        private static uint Hash(int seed, string id, string salt)
        {
            // FNV-1a over the seed, the id and the purpose: stable across runs and platforms,
            // unlike string.GetHashCode.
            unchecked
            {
                uint hash = 2166136261;
                void Mix(string text) { foreach (char c in text ?? string.Empty) { hash ^= c; hash *= 16777619; } hash ^= 0xFF; hash *= 16777619; }
                Mix(seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Mix(id);
                Mix(salt);
                return hash;
            }
        }

        // ---------------------------------------------------------------- the reference build's lines

        private static readonly IReadOnlyDictionary<string, string[]> Intro = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Strategic"] = new[] { "I'm not here to make friends… well, maybe a few.", "Every conversation is a chess move. Let's play." },
            ["Competitive"] = new[] { "Second place is just the first loser. I'm here to win.", "I don't do 'good game.' I do 'game over.'" },
            ["Loyal"] = new[] { "Once you're in my circle, I've got your back 'til the end.", "Loyalty isn't a strategy for me — it's who I am." },
            ["Social"] = new[] { "I just love meeting new people! This is going to be amazing!", "The more friends, the better. Let's make this house a party!" },
            ["Charming"] = new[] { "They say charm is a super-power. Guess I'm your hero.", "I can talk my way out of anything — or into anything." },
            ["Manipulative"] = new[] { "People are predictable. That's my advantage.", "Information is currency, and I plan to be rich." },
            ["Analytical"] = new[] { "I've studied every season. I know exactly what it takes.", "Numbers don't lie, and neither does my game plan." },
            ["Emotional"] = new[] { "I wear my heart on my sleeve, and that's my strength.", "Real connections beat fake alliances every time." },
            ["Funny"] = new[] { "If I can make you laugh, you won't vote me out. Simple math.", "Life's too short to be serious — especially in this house!" },
            ["Deceptive"] = new[] { "You'll never know what I'm really thinking. That's the point.", "The best lies have a little truth mixed in." },
            ["Introverted"] = new[] { "I'm quiet, but I notice everything. Everything.", "Don't mistake my silence for weakness." },
            ["Stubborn"] = new[] { "When I set my mind on something, good luck changing it.", "They call it stubborn, I call it determined." },
            ["Flexible"] = new[] { "I go with the flow — wherever the power is.", "Adaptability is the ultimate weapon in this game." },
            ["Intuitive"] = new[] { "I've got a gut feeling about this house. Trust me.", "I can read a room before anyone says a word." },
            ["Sneaky"] = new[] { "I'll be the last person you suspect. That's the plan.", "Fly under the radar, strike when it counts." },
            ["Confrontational"] = new[] { "If you come for me, you'd better not miss.", "I say what everyone else is thinking. Deal with it." },
            ["Impulsive"] = new[] { "I go with my gut! No overthinking for me.", "Life's more fun when you don't plan everything." },
        };

        private static readonly string[] DefaultIntro =
        {
            "Hey everyone, excited to be here! Let's have a great time.",
            "Ready to make some moves and meet some amazing people!",
        };

        private static readonly IReadOnlyDictionary<string, string[]> AllStarIntro = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Dan Gheesling"] = new[] { "Welcome to the mist. By the time you figure out my game, it'll be too late.", "I coach football — reading people and calling plays is what I do.", "Dan's Funeral was just the beginning. I've got a whole new playbook." },
            ["Dr. Will Kirby"] = new[] { "I hate you all. ...I'm kidding. Or am I?", "I've never won a competition and I never will. That's not a weakness — it's a flex.", "Chilltown is always open for new members. Maybe." },
            ["Janelle Pierzina"] = new[] { "Floaters, grab a life vest! ...Just kidding. For now.", "I've been here before. Three times. And I'm still standing.", "Comp wins speak louder than words. Remember that." },
            ["Rachel Reilly"] = new[] { "No one gets between me and this game! NO ONE!", "I'm not here to make friends — actually, I might cry about that later.", "Floaters, you better grab a life vest... oh wait, that's Janelle's line. NOBODY COMES BETWEEN ME AND MY MAN!" },
            ["Derrick Levasseur"] = new[] { "Nice to meet you. I'm just a regular guy. Nothing to see here.", "I like to observe before I speak. Old habit.", "Undercover work taught me one thing: the best player is the one nobody suspects." },
            ["Tyler Crispen"] = new[] { "Dude, this house is sick! Stoked to be here, bro.", "I'm just a chill surfer dude. Don't worry about me.", "I'll ride any wave this game throws at me. Let's go!" },
            ["Vanessa Rousso"] = new[] { "Mathematically speaking, my odds of winning are very good.", "I read people for a living. Poker faces don't work on me.", "Emotions are just data points. I process them all." },
            ["Cody Calafiore"] = new[] { "I learned from the best. This time I'm finishing what I started.", "Loyalty is everything. I don't flip, period.", "Last time I took someone else to the end. Not this time." },
            ["Danielle Reyes"] = new[] { "I've been reading people since before half of you were born.", "I smile sweetly, but make no mistake — I'm always calculating.", "The original mastermind is back. Take notes." },
            ["Xavier Prather"] = new[] { "I'm an attorney. I build cases, and I win them.", "I don't need to be the loudest in the room to be the most dangerous.", "Strategy is about patience. I've got plenty." },
            ["Jun Song"] = new[] { "I'll be in the kitchen. That's where all the real conversations happen.", "Everyone underestimates the floater. That's exactly what I want.", "I won by floating to power, not fighting it. Some of you should try it." },
            ["Chelsie Baham"] = new[] { "I connect with people — that's my superpower.", "I'm patient. The right moment always comes if you wait for it.", "Being genuine isn't a weakness. It's how you build an army." },
        };

        // Indexed by Outcome: match, neutral, clash.
        private static readonly IReadOnlyDictionary<string, string[][]> Reactions = new Dictionary<string, string[][]>(StringComparer.Ordinal)
        {
            ["Strategic"] = new[] { new[] { "Now you're speaking my language.", "Smart. I like that." }, new[] { "We'll see how things play out.", "Interesting approach." }, new[] { "...Okay. Sure.", "That's... one way to go about it." } },
            ["Competitive"] = new[] { new[] { "Hell yeah! Game on!", "I respect that energy!" }, new[] { "Cool. Let's see what you've got.", "Fair enough." }, new[] { "You don't seem like much competition.", "Hmm. Alright." } },
            ["Loyal"] = new[] { new[] { "I can already tell we're gonna be close!", "That means a lot!" }, new[] { "Nice to meet you too.", "We'll see where this goes." }, new[] { "That felt a little... cold.", "I don't know about you yet." } },
            ["Social"] = new[] { new[] { "Oh my gosh, I love you already!", "This is going to be SO fun!" }, new[] { "Cool cool! Welcome to the house!", "Hey, nice!" }, new[] { "Oh... okay. A bit intense.", "Hmm, you're hard to read." } },
            ["Charming"] = new[] { new[] { "Aw, aren't you sweet!", "I like your style!" }, new[] { "Smooth. I see you.", "Not bad, not bad." }, new[] { "A little forward, don't you think?", "Hmm, interesting choice." } },
            ["Manipulative"] = new[] { new[] { "Oh, I think we'll get along just fine.", "You understand the game." }, new[] { "Noted.", "I'll keep that in mind." }, new[] { "How... genuine. *eye roll*", "That's cute." } },
            ["Analytical"] = new[] { new[] { "I appreciate a strategic mind.", "Good. You're thinking ahead." }, new[] { "Interesting data point.", "I'll factor that in." }, new[] { "That was... emotionally driven.", "Not what I expected." } },
            ["Emotional"] = new[] { new[] { "That's so sweet! I feel it too!", "We're gonna be great friends!" }, new[] { "That's cool. I vibe with that.", "Nice energy." }, new[] { "That felt kinda fake...", "I can tell when someone's not real." } },
            ["Funny"] = new[] { new[] { "Ha! I like you already!", "Finally, someone fun around here!" }, new[] { "Heh, not bad. We'll see.", "You're alright, I guess!" }, new[] { "Whoa, lighten up a little!", "Tough crowd, huh?" } },
            ["Deceptive"] = new[] { new[] { "Clever. Very clever.", "I see what you did there." }, new[] { "Hmm, playing it safe? Smart.", "We'll see." }, new[] { "Bold move. Risky though.", "That was... transparent." } },
            ["Introverted"] = new[] { new[] { "...Thanks. That was nice.", "I appreciate the calm approach." }, new[] { "Hey.", "Nice to meet you." }, new[] { "That's a lot of energy...", "Can you tone it down?" } },
            ["Stubborn"] = new[] { new[] { "Respect. You've got guts!", "I like someone who stands firm." }, new[] { "We'll see if you back that up.", "Alright. Cool." }, new[] { "Don't try to play me.", "I see through that." } },
            ["Flexible"] = new[] { new[] { "Love the vibe! Let's keep it chill.", "We can definitely work together." }, new[] { "Cool, I'm easy-going.", "Whatever works!" }, new[] { "Whoa, a bit much. Let's relax.", "I don't vibe with that energy." } },
            ["Intuitive"] = new[] { new[] { "I had a feeling you'd be cool.", "My gut says we're good." }, new[] { "Hmm, I'm reading you...", "Interesting. I'll trust my instincts." }, new[] { "Something feels off about that.", "My gut says be careful." } },
            ["Sneaky"] = new[] { new[] { "Smart. You know when to be subtle.", "I like how you operate." }, new[] { "Okay. I'll watch you.", "Noted." }, new[] { "Way too obvious.", "That won't work on me." } },
            ["Confrontational"] = new[] { new[] { "Now THAT'S what I'm talking about!", "Bring it on!" }, new[] { "Alright, we'll see.", "Fair enough." }, new[] { "Ugh, spare me the niceties.", "Be real with me." } },
            ["Impulsive"] = new[] { new[] { "YES! Let's go! I'm pumped!", "I love the energy!" }, new[] { "Cool cool cool! Let's do this!", "Sure, why not!" }, new[] { "Overthinking it much?", "Loosen up a little!" } },
        };

        private static readonly IReadOnlyDictionary<string, string[][]> AllStarReactions = new Dictionary<string, string[][]>(StringComparer.Ordinal)
        {
            ["Dan Gheesling"] = new[] { new[] { "Welcome to the mist, my friend.", "I see potential in you. Let's talk strategy." }, new[] { "Interesting. I'll keep my eye on you.", "The mist works in mysterious ways." }, new[] { "Hmm. You might be a threat.", "I've dealt with bigger personalities. Trust me." } },
            ["Dr. Will Kirby"] = new[] { new[] { "I hate that I like you. This is a problem.", "Chilltown might have room for one more." }, new[] { "Meh. I've seen better, I've seen worse.", "You're... adequate. For now." }, new[] { "How delightfully awful. I love it.", "You remind me of someone I evicted. Twice." } },
            ["Janelle Pierzina"] = new[] { new[] { "I like your fire! We could dominate comps together.", "You've got that winner energy. I respect that." }, new[] { "We'll see if you can keep up.", "Not bad. But can you win when it counts?" }, new[] { "Ugh. Floater energy. Next!", "I've eaten people like you for breakfast. Three seasons' worth." } },
            ["Rachel Reilly"] = new[] { new[] { "OH MY GOD, I love you already! *squeals*", "We're going to be BEST friends! No one can stop us!" }, new[] { "Okay... we'll see. But don't cross me.", "I'm watching you. WITH BOTH EYES." }, new[] { "*dramatic gasp* HOW DARE YOU!", "You do NOT want me as your enemy. Trust. Me." } },
            ["Derrick Levasseur"] = new[] { new[] { "Good. Stay close. I've got a plan.", "You seem trustworthy. That's valuable." }, new[] { "Copy that. I'll file this away.", "Noted. Moving on." }, new[] { "Interesting approach. I'll remember that.", "You just showed me your cards. Bad move." } },
            ["Tyler Crispen"] = new[] { new[] { "Sick, dude! We're gonna vibe so hard.", "Bro, I'm stoked! Let's ride this wave together!" }, new[] { "Chill, chill. We're all good.", "Cool beans, man. No worries." }, new[] { "Whoa, intense vibes. Not my wavelength.", "Dude... that's a lot. Let's just chill." } },
            ["Vanessa Rousso"] = new[] { new[] { "The math checks out. We'd be a strong pair.", "I like your reasoning. Very logical." }, new[] { "Hmm, I need more data before I decide about you.", "Inconclusive. I'll recalculate later." }, new[] { "That was a -EV play. Poker term. Look it up.", "*tears up* That really hurt... strategically speaking." } },
            ["Cody Calafiore"] = new[] { new[] { "Ride or die. That's what I'm about. You in?", "I got your back. Day one. Let's go." }, new[] { "Alright. Prove yourself and we'll talk.", "I respect the hustle. Let's see where it goes." }, new[] { "Nah, I don't trust that energy.", "I've seen snakes before. I know the signs." } },
            ["Danielle Reyes"] = new[] { new[] { "Oh honey, I like you. Come sit with me.", "Smart move. The Black Widow approves." }, new[] { "Mmhmm. I see you.", "Interesting. Very interesting." }, new[] { "Chile... no. Just no.", "I've been playing this game since 2002. Try again." } },
            ["Xavier Prather"] = new[] { new[] { "Strong opening statement. I like it.", "Consider this an alliance offer. Pro bono." }, new[] { "I'll take that under advisement.", "The jury's still out on you. Pun intended." }, new[] { "Objection. That approach won't work on me.", "I rest my case. About you, I mean." } },
            ["Jun Song"] = new[] { new[] { "Pull up a chair. I made something. Let's talk.", "I like you. You're not trying too hard." }, new[] { "Want some food? I cook when I'm thinking.", "Mmm. I'll observe a bit more." }, new[] { "Yikes. Too aggressive for the kitchen.", "I'll just be over here... floating past you." } },
            ["Chelsie Baham"] = new[] { new[] { "I feel like we just clicked! This is great!", "You're my kind of person. Genuine energy." }, new[] { "That's cool! Let's get to know each other more.", "I'm open to seeing where this goes." }, new[] { "Hmm, that didn't land for me. But I'm patient.", "I'll give you another chance. Everyone deserves one." } },
        };
    }
}
