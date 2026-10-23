using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What a houseguest says when they come to the player in a role (ROLE-MOMENTS-PLAN R1, §3.0 "Quotes"):
    /// three authored lines for every tone each moment's speaker can wear (<see cref="RoleTone.TonesFor"/>),
    /// and the keyed pick between them.
    ///
    /// <para><b>The lines.</b> Each is at most <see cref="MostWords"/> words. None states terms: the terms line
    /// does, straight from the row ("In return: ..."), so a line names no number, price kind or vote. None
    /// promises what its moment does not keep: only a Desperate nominee asking for the veto says they will owe
    /// the player, because that moment's rider writes the favour (<see cref="RoleTone.Riders.OwesAFavour"/>,
    /// <see cref="TakesTheRiders"/>), and only a Threatening houseguest threatens. A line is true in every state
    /// that gives its tone: a Bitter line fits a nomination and a broken word alike, a Desperate one the endgame
    /// and a second time on the block alike, and a nominee's line fits one the player put up this week, whom the
    /// ladder counts only from the next. A pitch may name the target the pitcher recommends (<see cref="Target"/>),
    /// by first name; every pitch tone has a line that names nobody, which a pitch that recommends nobody says
    /// (<see cref="Line"/>). The lines start from the plan's own in §3.1 to §3.5.</para>
    ///
    /// <para><b>The pick.</b> <see cref="StoryRandom.Index"/> on "role-quote:" and the occasion's own id - the
    /// ask's deal id, the pitch's card id, the moment's storyline id - so a reload, a re-render or a replay shows
    /// the same line, nothing is stored and the season's stream is never touched.</para>
    ///
    /// <para>Pure and read-only. The offer board draws the veto ask's lines (R2, <see cref="RoleOffers"/>).</para>
    /// </summary>
    public static class RoleQuotes
    {
        /// <summary>The moments a quote is said at: R2 to R6's, and the holder's offer (R8).</summary>
        public const string VetoAsk = "veto-ask", KeepTheBlock = "keep-the-block", Pitch = "pitch",
            HolderOffer = "holder-offer", QuietTears = "quiet-tears";

        public static readonly string[] Moments = { VetoAsk, KeepTheBlock, Pitch, HolderOffer, QuietTears };

        /// <summary>
        /// The role the speaker is in at each moment: a nominee asks for the veto, and is the one found in tears;
        /// the Head of Household asks to keep the block; a houseguest pitches to a Head of Household player; the
        /// veto holder makes the offer. Null for a moment this does not know.
        /// </summary>
        public static string RoleOf(string moment)
        {
            switch (moment)
            {
                case VetoAsk: case QuietTears: return RoleTone.Role.Nominee;
                case KeepTheBlock: return RoleTone.Role.HeadOfHousehold;
                case Pitch: return RoleTone.Role.Courting;
                case HolderOffer: return RoleTone.Role.VetoHolder;
                default: return null;
            }
        }

        /// <summary>
        /// Whether the tone table's riders settle on this moment (§3.0): the offers the player accepts and keeps
        /// or turns down - a nominee's veto ask (§3.2) and the Head of Household's keep-the-block (§3.3). A pitch
        /// has its own consequences (§3.1), the holder's column has no rider (§3.4b), and quiet tears is a story
        /// moment with its own options (§3.5).
        /// </summary>
        public static bool TakesTheRiders(string moment) => moment == VetoAsk || moment == KeepTheBlock;

        /// <summary>Lines for each tone of each moment.</summary>
        public const int LinesPerPair = 3;

        /// <summary>The most words a line may have, its target counted as <see cref="SomeoneElse"/>.</summary>
        public const int MostWords = 15;

        /// <summary>Where a pitch names the houseguest they recommend.</summary>
        public const string Target = "{TARGET}";

        /// <summary>
        /// The target as <see cref="Named"/> says it of nobody. <see cref="Line"/> never needs it - a pitch that
        /// recommends nobody says a line that names nobody - so it is the fallback, and the longest a target can run.
        /// </summary>
        public const string SomeoneElse = "someone else";

        /// <summary>The front of every quote's key: <c>"role-quote:" + occasionId</c>.</summary>
        public const string KeyPrefix = "role-quote:";

        private static readonly Dictionary<string, Dictionary<string, string[]>> Catalogue =
            new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal)
        {
            // A nominee asking the player, who holds the veto, to use it on them (§3.2).
            {
                VetoAsk, new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    { RoleTone.Bitter, new[] {
                        "After what you did, the least you can do is pull me off.",
                        "You burned me before. Don't leave me sitting up here too.",
                        "I haven't forgotten what you did. This is your chance to make it right." } },
                    { RoleTone.Threatening, new[] {
                        "Leave me up there and I won't forget it.",
                        "Leave me on that block and you'll have made an enemy.",
                        "I'd think very carefully before you leave me sitting up here." } },
                    { RoleTone.Desperate, new[] {
                        "Save me and I'll owe you. I mean it.",
                        "Please. I can't go home this week. Pull me off the block.",
                        "I'm running out of chances in here. Please use it on me." } },
                    { RoleTone.Friendly, new[] {
                        "We're in this together. Pull me off the block.",
                        "You're among the few people I trust in here. Please save me.",
                        "I know you're on my side. Please use it on me." } },
                    { RoleTone.Earnest, new[] {
                        "I'm not asking for a miracle, just a chance to stay.",
                        "I've loved every day in this house. Please don't let it end.",
                        "I'm asking you honestly, with nothing up my sleeve. Please save me." } },
                    { RoleTone.Calculated, new[] {
                        "Run the numbers. Me staying is better for your game.",
                        "Using it on me is the smart play. You know it is.",
                        "Keep me here and you keep a useful piece on the board." } },
                    { RoleTone.Pragmatic, new[] {
                        "I know we haven't always seen eye to eye, but save me.",
                        "No speeches. I'm asking you to use the veto on me.",
                        "You hold the veto, I'm on the block. Help me out here." } },
                }
            },
            // The Head of Household asking the player not to use the veto (§3.3).
            {
                KeepTheBlock, new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    { RoleTone.Bitter, new[] {
                        "You've crossed me before. Don't do it again with that veto.",
                        "After everything between us, the least you can do is leave my block alone.",
                        "You owe me this much. Leave the nominations the way I set them." } },
                    { RoleTone.Threatening, new[] {
                        "Use that veto and you'll have made yourself a problem.",
                        "Touch my nominations and I'll take it very personally.",
                        "Think about who you'd be crossing before you use that veto." } },
                    { RoleTone.Friendly, new[] {
                        "I put them up for a reason. Trust my game.",
                        "We're on the same side here. Let my block stand.",
                        "You know where I stand with you. Leave the nominations be." } },
                    { RoleTone.Earnest, new[] {
                        "I thought hard about this block. Please leave it as it is.",
                        "This wasn't easy for me. Please don't make me do it again.",
                        "I'm being honest with you. I want this block to stay as it is." } },
                    { RoleTone.Calculated, new[] {
                        "I'm asking you not to change my nominations.",
                        "Leaving the block alone is the clean move for both of us.",
                        "The block is set the way it needs to be. Don't disturb it." } },
                    { RoleTone.Pragmatic, new[] {
                        "Keep it simple. Leave the nominations alone.",
                        "No drama this week. Just leave the veto in your pocket.",
                        "Let it ride. The block is fine the way it is." } },
                }
            },
            // A houseguest pitching to the player as Head of Household, before the nominations (§3.1).
            {
                Pitch, new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    { RoleTone.Bitter, new[] {
                        "You've burned me before. Don't do it again with your nominations.",
                        "After what you did, you owe me. Keep me off the block.",
                        "Put {TARGET} up, not me. I think you owe me that much." } },
                    { RoleTone.Threatening, new[] {
                        "Put me up and you'd better win next week too.",
                        "Put {TARGET} up. Put me up and you'll regret it.",
                        "Leave me off the block. You don't want me as an enemy." } },
                    { RoleTone.Desperate, new[] {
                        "Please don't put me up. I can't afford a week on the block.",
                        "Please, put {TARGET} up instead. I need to stay off the block.",
                        "I'm begging you. Keep me off the block this week." } },
                    { RoleTone.Friendly, new[] {
                        "You know I'm with you. Put {TARGET} up and keep me off it.",
                        "We look out for each other. Leave me off the block this week.",
                        "I'm on your side. {TARGET} is who should go up." } },
                    { RoleTone.Earnest, new[] {
                        "I'm no threat to you. I'd put {TARGET} up.",
                        "I'm being straight with you. Please don't put me up this week.",
                        "I really want to stay. Please put {TARGET} up instead of me." } },
                    { RoleTone.Calculated, new[] {
                        "{TARGET} is the bigger problem for both of us.",
                        "Put {TARGET} up. It's the move that helps us both.",
                        "Look at the numbers. Putting me up does nothing for your game." } },
                    { RoleTone.Pragmatic, new[] {
                        "I'd put {TARGET} up. Keep me safe.",
                        "Leave me out of it this week. That's all I'm asking.",
                        "Here's my pitch: {TARGET} goes up, and I stay off the block." } },
                }
            },
            // The veto holder offering to use it on a nominated player, for a price (§3.4b, R8).
            {
                HolderOffer, new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    { RoleTone.Bitter, new[] {
                        "You put me through it before. This will cost you.",
                        "After what you did to me, you're lucky I'm offering anything.",
                        "I haven't forgotten what you did. You want it, it has a price." } },
                    { RoleTone.Friendly, new[] {
                        "I want to pull you off. I just need something back.",
                        "You know I'm in your corner. I just need something in exchange.",
                        "I'm happy to use it on you. Help me out too." } },
                    { RoleTone.Earnest, new[] {
                        "I'd like to use it on you. Meet me halfway.",
                        "I honestly want to pull you off. I just can't do it for nothing.",
                        "This matters to me. I want to save you, but I need something too." } },
                    { RoleTone.Calculated, new[] {
                        "I could use it on you. Here's what it costs.",
                        "Everything in here has a price. The veto does too.",
                        "Let's be practical. I pull you off, you give me something back." } },
                    { RoleTone.Pragmatic, new[] {
                        "It's yours, for a price.",
                        "I can use it on you. Let's talk about what I get.",
                        "Nothing personal. I'll use it on you if the price is right." } },
                }
            },
            // A nominee the player finds crying quietly in the living room (§3.5). The player may have put them up this
            // week, which no tone counts until the next, so no line welcomes the player as the one they hoped to see.
            {
                QuietTears, new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    { RoleTone.Bitter, new[] {
                        "You're the last person I want seeing me like this.",
                        "Of all the people to walk in. Please, just leave me be.",
                        "Don't pretend you care. You know what you did." } },
                    { RoleTone.Threatening, new[] {
                        "I'm fine. And I'm not done in this game, not even close.",
                        "Don't look at me like that. I'll be back on my feet.",
                        "Crying doesn't mean I'm finished. Remember that." } },
                    { RoleTone.Desperate, new[] {
                        "I can't go home yet. I'm not ready.",
                        "I keep thinking this might be my last week here.",
                        "I don't know how many more of these weeks I've got." } },
                    { RoleTone.Friendly, new[] {
                        "I know it's only a game. I just need a minute.",
                        "Thanks for coming over. It's been a really rough day.",
                        "Stay if you want. Today has been a lot." } },
                    { RoleTone.Earnest, new[] {
                        "Sorry. I didn't want anyone to see me like this.",
                        "I just miss home, and this week is so hard.",
                        "I'm giving this game everything I have. It's just a lot right now." } },
                    { RoleTone.Calculated, new[] {
                        "It's nothing. I'm just thinking through my week.",
                        "I'm fine. Just letting it out before I get back to work.",
                        "Give me a moment. I'm working out what comes next." } },
                    { RoleTone.Pragmatic, new[] {
                        "It's been a long day. I'll be fine.",
                        "Don't worry about me. I just needed a minute.",
                        "It's the block. It gets to everyone eventually." } },
                }
            },
        };

        /// <summary>A moment's lines for one tone, as authored; empty for a pair the catalogue does not hold.</summary>
        public static IReadOnlyList<string> Lines(string moment, string tone) =>
            moment != null && tone != null && Catalogue.TryGetValue(moment, out var tones) && tones.TryGetValue(tone, out var lines)
                ? Array.AsReadOnly(lines) : Array.AsReadOnly(new string[0]);

        /// <summary>The tones a moment has lines for, in the ladder's order.</summary>
        public static IEnumerable<string> TonesWithLines(string moment) =>
            moment != null && Catalogue.TryGetValue(moment, out var tones) ? RoleTone.Ladder.Where(tones.ContainsKey) : Enumerable.Empty<string>();

        /// <summary>A line's words, its target counted as <see cref="SomeoneElse"/>: the most it can come to.</summary>
        public static int WordCount(string line) => string.IsNullOrWhiteSpace(line) ? 0
            : line.Replace(Target, SomeoneElse).Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

        /// <summary>Which of a pair's lines an occasion shows: <see cref="StoryRandom.Index"/> on its key.</summary>
        public static int Index(EpisodeState s, string occasionId) => StoryRandom.Index(s, KeyPrefix + occasionId, LinesPerPair);

        /// <summary>Whether a line names the target a pitch recommends (<see cref="Target"/>).</summary>
        public static bool NamesTheTarget(string line) => line != null && line.IndexOf(Target, StringComparison.Ordinal) >= 0;

        /// <summary>
        /// The line an occasion shows for a moment and a tone, with the target named; null for no occasion or a
        /// pair the catalogue does not hold. With no target to name, the pick steps on from a line that names one
        /// to the next that does not, so a pitch that recommends nobody never says "someone else".
        /// </summary>
        public static string Line(EpisodeState s, string moment, string tone, string occasionId, string targetId = null)
        {
            if (s == null || string.IsNullOrEmpty(occasionId)) return null;
            var lines = Lines(moment, tone);
            if (lines.Count == 0) return null;
            int index = Index(s, occasionId);
            if (TargetName(s, targetId) == null)
                for (int step = 0; step < lines.Count && NamesTheTarget(lines[index]); step++) index = (index + 1) % lines.Count;
            return Named(s, lines[index], targetId);
        }

        /// <summary>The line a houseguest says at a moment: their tone in its role (<see cref="RoleTone.Of"/>), then the occasion's pick.</summary>
        public static string Of(EpisodeState s, string moment, string speakerId, string occasionId, string targetId = null)
        {
            string role = RoleOf(moment);
            return role == null ? null : Line(s, moment, RoleTone.Of(s, speakerId, role), occasionId, targetId);
        }

        /// <summary>A line with its target named by first name, or as <see cref="SomeoneElse"/>; capitalised where it opens the line.</summary>
        public static string Named(EpisodeState s, string line, string targetId)
        {
            if (!NamesTheTarget(line)) return line;
            string text = line.Replace(Target, TargetName(s, targetId) ?? SomeoneElse);
            return char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text.Substring(1) : text;
        }

        /// <summary>The target's first name, or null when the id names nobody in the season.</summary>
        private static string TargetName(EpisodeState s, string targetId)
        {
            var target = string.IsNullOrEmpty(targetId) ? null : s?.Find(targetId);
            string name = target == null ? null : FinalistRead.FirstName(target.name);
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }
}
