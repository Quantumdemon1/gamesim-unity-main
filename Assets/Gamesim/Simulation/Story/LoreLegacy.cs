using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Legacy sheets for the twelve All-Stars cards: real people under their real names.
    ///
    /// <para><b>Game register only</b> (20 §3.10). Their documented record, how they play, what
    /// they respect across a table, and what sets them off in a game - nothing about family,
    /// romance or a secret, and nothing the record does not show. Every season result here is a
    /// matter of public record; where a card's own bio is loose with a detail, the sheet says less
    /// rather than repeating it. The playstyles follow the Big Brother build's All-Stars profiles
    /// (<c>src/data/allstars-templates.ts</c>), paraphrased and trimmed of anything romantic.</para>
    /// </summary>
    public static partial class Lore
    {
        private static IEnumerable<Sheet> LegacySheets()
        {
            yield return LegacySheet("dan-gheesling", "Strategic", "cold", true,
                G("legacy", Facets.Legacy, 1, "Won his first season with every jury vote, and reached the final two again in his second."),
                G("respects", Facets.Respects, 2, "A move with a plan behind it. He will admire one even when it is aimed at him.", Calculated, 1),
                G("hot", Facets.HotButton, 2, "Being called out on a move before it lands.", Candid, -1, "exposure"),
                G("conflict", Facets.ConflictStyle, 1, "Gets calmer the more cornered he is."),
                G("tendency", Facets.Tendency, 1, "Gets people to carry his game for him, and lets them think it was their idea."));

            yield return LegacySheet("dr-will-kirby", "Charming", "even", true,
                G("legacy", Facets.Legacy, 1, "Won his season while telling the house, more or less openly, that he was lying to them."),
                G("respects", Facets.Respects, 2, "Someone who plays along with the bit.", Playful, 1),
                G("hot", Facets.HotButton, 2, "Earnest speeches about honesty.", Candid, -1, "sincerity"),
                G("conflict", Facets.ConflictStyle, 1, "Laughs at the accusation, agrees with it, and changes the subject."),
                G("tendency", Facets.Tendency, 1, "Tells you he is lying, and somehow you keep him anyway."));

            yield return LegacySheet("derrick-levasseur", "Analytical", "cold", true,
                G("legacy", Facets.Legacy, 1, "Won his season without once being nominated."),
                G("respects", Facets.Respects, 2, "Someone who can keep a straight face.", Calculated, 1),
                G("hot", Facets.HotButton, 2, "Being read out loud.", Candid, -1, "exposure"),
                G("conflict", Facets.ConflictStyle, 1, "Never raises his voice. Asks another question."),
                G("tendency", Facets.Tendency, 1, "Runs a conversation by asking the questions in it."));

            yield return LegacySheet("janelle-pierzina", "Competitive", "loud", false,
                G("legacy", Facets.Legacy, 1, "Has played more than once, and has always been the competitor the house plans around."),
                G("respects", Facets.Respects, 2, "Someone who plays to win, and wins.", Bold, 1),
                G("hot", Facets.HotButton, 2, "Floaters. She has no patience for anyone hiding in the middle.", Yield, -1, "floating"),
                G("conflict", Facets.ConflictStyle, 1, "Calls it out, loudly, to your face."),
                G("tendency", Facets.Tendency, 1, "Does not betray a ride-or-die."));

            yield return LegacySheet("cody-calafiore", "Loyal", "even", true,
                G("legacy", Facets.Legacy, 1, "Finished runner-up in his first season, and won All-Stars with every jury vote."),
                G("respects", Facets.Respects, 2, "Someone who keeps their word when it costs them.", Candid, 1),
                G("hot", Facets.HotButton, 2, "Being played. A double-cross is not something he forgets.", Hardball, -1, "betrayal"),
                G("conflict", Facets.ConflictStyle, 1, "Quiet, then decisive."),
                G("tendency", Facets.Tendency, 1, "Picks one person to trust completely, and wins beside them."));

            yield return LegacySheet("danielle-reyes", "Strategic", "cold", false,
                G("legacy", Facets.Legacy, 1, "Reached the final two in her first season and came back for All-Stars. Her reads on people are still talked about."),
                G("respects", Facets.Respects, 2, "Someone who trusts their gut.", Warm, 1),
                G("hot", Facets.HotButton, 2, "Being asked to prove a read.", Calculated, -1, "proof"),
                G("conflict", Facets.ConflictStyle, 1, "Keeps her reads to herself until they matter."),
                G("tendency", Facets.Tendency, 1, "Reads the room before anyone else knows there is anything to read."));

            yield return LegacySheet("vanessa-rousso", "Analytical", "loud", false,
                G("legacy", Facets.Legacy, 1, "Played the numbers out loud all the way to a third-place finish."),
                G("respects", Facets.Respects, 2, "Someone who can explain their odds.", Calculated, 1),
                G("hot", Facets.HotButton, 2, "Being lied to. She will confront it, hard.", Hardball, -1, "lying"),
                G("conflict", Facets.ConflictStyle, 1, "Confronts people aggressively when she catches them lying."),
                G("tendency", Facets.Tendency, 1, "Works out everyone's odds out loud, then bets against the version they showed her."));

            yield return LegacySheet("tyler-crispen", "Charming", "even", false,
                G("legacy", Facets.Legacy, 1, "Ran several alliances at once to a runner-up finish, and was voted the audience's favourite."),
                G("respects", Facets.Respects, 2, "Someone easy to be around.", Playful, 1),
                G("hot", Facets.HotButton, 2, "Confrontation. He will do almost anything to avoid one.", Bold, -1, "confrontation"),
                G("conflict", Facets.ConflictStyle, 1, "Avoids it, smooths it over, and moves on."),
                G("tendency", Facets.Tendency, 1, "Never looks like a threat, for as long as that stays useful."));

            yield return LegacySheet("chelsie-baham", "Strategic", "cold", true,
                G("legacy", Facets.Legacy, 1, "Won her season, trusted by every side of the house until the moment it mattered."),
                G("respects", Facets.Respects, 2, "Someone who can keep a secret.", Calculated, 1),
                G("hot", Facets.HotButton, 2, "Being asked, point blank, which side she is on.", Candid, -1, "sides"),
                G("conflict", Facets.ConflictStyle, 1, "Stays out of the fight, and strikes later."),
                G("tendency", Facets.Tendency, 1, "Knows every side's plan, and picks which side finds out last."));

            yield return LegacySheet("rachel-reilly", "Confrontational", "loud", true,
                G("legacy", Facets.Legacy, 1, "Won on her second try, after a first season the house still talks about."),
                G("respects", Facets.Respects, 2, "Someone who says it to her face.", Hardball, 1),
                G("hot", Facets.HotButton, 2, "Being told to calm down.", Yield, -1, "calm down"),
                G("conflict", Facets.ConflictStyle, 1, "Loud, immediate, and usually over by dinner."),
                G("tendency", Facets.Tendency, 1, "Takes every shot in front of her, loudly, and makes the house deal with the noise."));

            yield return LegacySheet("xavier-prather", "Strategic", "even", true,
                G("legacy", Facets.Legacy, 1, "Won his season as part of an alliance that stayed together all the way to the end."),
                G("respects", Facets.Respects, 2, "Loyalty with a plan behind it.", Candid, 1),
                G("hot", Facets.HotButton, 2, "Chaos. He does not raise his voice, and does not trust people who do.", Bold, -1, "chaos"),
                G("conflict", Facets.ConflictStyle, 1, "Stays calm and talks it through."),
                G("tendency", Facets.Tendency, 1, "Holds an alliance together without ever raising his voice."));

            yield return LegacySheet("jun-song", "Sneaky", "cold", true,
                G("legacy", Facets.Legacy, 1, "Won her season by staying out of every war until the end."),
                G("respects", Facets.Respects, 2, "Someone who can keep a secret.", Calculated, 1),
                G("hot", Facets.HotButton, 2, "Being caught listening.", Hardball, -1, "being caught"),
                G("conflict", Facets.ConflictStyle, 1, "Stays out of it, and lets the house burn itself out."),
                G("tendency", Facets.Tendency, 1, "Uses cooking as a social tool: whoever is in the kitchen with her is talking."));
        }

        private static Sheet LegacySheet(string templateId, string primaryTrait, string conflictStyle, bool wonSeason, params Fact[] facts) =>
            new Sheet
            {
                key = templateId, source = LoreSources.Legacy, primaryTrait = primaryTrait, romance = null,
                conflictStyle = conflictStyle, goal = null, wonSeason = wonSeason,
                facts = facts.Select(f => { f.id = templateId + ":" + f.id; return f; }).ToArray(),
            };

        /// <summary>A documented, game-register fact about a real person.</summary>
        private static Fact G(string id, string facet, int depth, string text, string approach = null, int sign = 0, string topic = null) =>
            new Fact { id = id, facet = facet, depth = depth, text = text, approach = approach, sign = sign, topic = topic };
    }
}
