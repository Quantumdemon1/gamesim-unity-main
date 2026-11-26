using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The voice a houseguest answers in (plan 32 §G3), from their traits alone, the first match winning: Desperate for a
    /// nominee talking about their own fate (never the vote answer, which asks voters only), Blunt for Bold ≥ 2, Warm for
    /// Warm ≥ 2, Chatty for Sociable ≥ 2, Guarded for Steady ≥ 2 with Warm ≤ 0 or an Introvert, and Plain for everyone else.
    /// No voice reads the Honest axis, so a voice never gives a liar away. The All-Stars speak in the same trait voices.
    /// </summary>
    public static class Voice
    {
        public const string Desperate = "desperate", Blunt = "blunt", Warm = "warm", Chatty = "chatty", Guarded = "guarded", Plain = "plain";
        public static readonly string[] All = { Desperate, Blunt, Warm, Chatty, Guarded, Plain };

        public static string Of(ContestantState who, bool nomineeOnTheirOwnFate)
        {
            if (who == null) return Plain;
            if (nomineeOnTheirOwnFate) return Desperate;
            var a = Personality.Of(who);
            if (a.Bold >= 2) return Blunt;
            if (a.Warm >= 2) return Warm;
            if (a.Sociable >= 2) return Chatty;
            if ((a.Steady >= 2 && a.Warm <= 0) || who.traits.Contains("Introverted")) return Guarded;
            return Plain;
        }
    }

    /// <summary>
    /// What houseguests say back (plan 32 §K), shaped like <see cref="RoleQuotes"/>: banks keyed by question, voice and
    /// manner, exactly three lines a slot, each at most fifteen words, in the game's register and never a person's own
    /// catchphrase. <c>{NAME}</c> is the person a line names. A logged line is filled when it is made
    /// (<see cref="Fill"/>): the first name, "you" for the player, capitalised where it opens the line.
    ///
    /// <para>S1 holds the vote bank (Stack A, 75 lines), the read's two lines and the notebook's words. A bank freezes with
    /// its rules version: once <see cref="StoryRules.Current"/> reaches 12, a change to these lines needs a new level.</para>
    ///
    /// <para>Which line of a slot a vote answer takes freezes with it (<see cref="VoteLineIndex"/>): no two voters in one
    /// voice and manner give the same line in a week, and nobody gives last week's line again in the same manner, while the
    /// slot has a line left. Each answer's line is on the record (<see cref="EpisodeEngine.LinePrefix"/>), so the notebook
    /// quotes the words the log gave and a later answer never changes an earlier one.</para>
    /// </summary>
    public static class HouseAnswers
    {
        public const string Name = "{NAME}";
        public const int LinesASlot = 3, MostWords = 15;

        /// <summary>The read's word on a claim this week (§G4): what the player's read of a voter says of what they told them.</summary>
        public const string ReadDoubt = "Something about what they told you doesn't sit right.";
        public const string ReadHolds = "What they told you seems to hold.";

        /// <summary>The notebook's line beside an answer in a manner that voter lied in before (§G4).</summary>
        public const string LastTimeTheyLied = "Last time they sounded like this, they lied.";

        /// <summary>The vote bank: voice, then manner, then three lines. Stated manners name {NAME}; the neutral ones name nobody.</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string[]>> VoteBank =
            new Dictionary<string, IReadOnlyDictionary<string, string[]>>
            {
                {
                    Voice.Blunt, new Dictionary<string, string[]>
                    {
                        { Manner.Definite, new[] { "{NAME}. Done deal.", "{NAME}. That's it, that's the answer.", "It's {NAME}. Simple." } },
                        { Manner.Hedged, new[] { "Probably {NAME}? Don't hold me to it.", "{NAME}, I think. Mostly.", "Leaning {NAME}. For now, anyway." } },
                        { Manner.TurnedBack, new[] { "Why, who are you voting? ...Fine, {NAME}.", "You first. ...Okay, {NAME}.", "What's it to you? ...{NAME}, I guess." } },
                        { Manner.LaidOnThick, new[] { "{NAME}. Write it down. Lock it in.", "{NAME}, no question. Never been more sure.", "{NAME}. One hundred, two hundred percent." } },
                        { Manner.Deflect, new[] { "Not telling you that.", "That's my business.", "Nope. Next question." } },
                    }
                },
                {
                    Voice.Warm, new Dictionary<string, string[]>
                    {
                        { Manner.Definite, new[] { "{NAME}. I'm sorry, but that's where I am.", "Honestly? {NAME}. I wanted you to hear it from me.", "{NAME}. I'd rather tell you straight." } },
                        { Manner.Hedged, new[] { "I think {NAME}? It doesn't feel good.", "Maybe {NAME}? It's all so hard.", "Probably {NAME}... I hate this part." } },
                        { Manner.TurnedBack, new[] { "Where's your head at? ...Okay, {NAME}.", "Tell me yours first? ...Fine, {NAME}.", "Are you worried? ...It's {NAME}, I think." } },
                        { Manner.LaidOnThick, new[] { "{NAME}. I promise, I promise, I promise.", "{NAME}, totally. You can trust me on this one.", "{NAME}. I'd never lie to you about this." } },
                        { Manner.NonAnswer, new[] { "I really don't know yet. I'm sorry.", "I haven't decided. I'm not hiding anything.", "My head's a mess about it, honestly." } },
                    }
                },
                {
                    Voice.Chatty, new Dictionary<string, string[]>
                    {
                        { Manner.Definite, new[] { "{NAME}. And everybody I talk to says the same.", "Oh, {NAME}. Easy. Did you hear about earlier?", "{NAME}, obviously. Anyway, what's going on with you?" } },
                        { Manner.Hedged, new[] { "So, probably {NAME}? Things change every hour in here.", "Okay, maybe {NAME}? Don't quote me, people are talking.", "I'm thinking {NAME}? Ish? It's a whole thing." } },
                        { Manner.TurnedBack, new[] { "Wait, who are you voting? ...Okay fine, {NAME}.", "Ooh, are you fishing? ...{NAME}, I suppose.", "What have you heard? ...Okay, {NAME}." } },
                        { Manner.LaidOnThick, new[] { "{NAME}! Totally, completely, a thousand percent.", "{NAME}, obviously, everybody knows, it's so obvious.", "Oh, {NAME}. Like, no doubt. Zero. None." } },
                        { Manner.NonAnswer, new[] { "Oh, it's all over the place right now. Ask me later?", "Honestly, every conversation changes my mind.", "So many moving parts. I genuinely can't say yet." } },
                    }
                },
                {
                    Voice.Guarded, new Dictionary<string, string[]>
                    {
                        { Manner.Definite, new[] { "{NAME}.", "{NAME}. That's where I am.", "It'll be {NAME}." } },
                        { Manner.Hedged, new[] { "Probably {NAME}.", "{NAME}, most likely. We'll see.", "Leaning {NAME}. Could change." } },
                        { Manner.TurnedBack, new[] { "Why do you ask? ...{NAME}.", "Who wants to know? ...{NAME}, I suppose.", "What's your read? ...{NAME}, then." } },
                        { Manner.LaidOnThick, new[] { "{NAME}. Definitely. Absolutely definitely.", "{NAME}. No doubt at all. None.", "{NAME}. Completely certain. Completely." } },
                        { Manner.Deflect, new[] { "Ask me after.", "I keep that to myself.", "We'll see on the night." } },
                    }
                },
                {
                    Voice.Plain, new Dictionary<string, string[]>
                    {
                        { Manner.Definite, new[] { "{NAME}. Not losing sleep over it.", "{NAME}. That's my vote.", "Pretty set on {NAME}." } },
                        { Manner.Hedged, new[] { "Probably {NAME}? It's early, I mean...", "{NAME}, I think? Maybe.", "Leaning {NAME}, I guess." } },
                        { Manner.TurnedBack, new[] { "What are you thinking? ...{NAME}, I guess.", "Why, what've you heard? ...{NAME}.", "You tell me first. ...Okay, {NAME}." } },
                        { Manner.LaidOnThick, new[] { "{NAME}. A hundred percent. Swear on everything.", "{NAME}, for sure. For sure for sure.", "{NAME}. Totally. No doubt in my mind." } },
                        { Manner.NonAnswer, new[] { "Honestly? I keep going back and forth.", "Haven't made my mind up.", "Still thinking it over." } },
                    }
                },
            };

        /// <summary>The notebook's few words for a manner: "(they hedged)".</summary>
        public static string MannerWords(string manner)
        {
            switch (manner)
            {
                case Manner.Definite: return "they didn't blink";
                case Manner.Hedged: return "they hedged";
                case Manner.TurnedBack: return "they turned it back on you";
                case Manner.LaidOnThick: return "they laid it on thick";
                case Manner.NonAnswer: return "they wouldn't commit";
                case Manner.Deflect: return "they brushed it off";
                default: return null;
            }
        }

        /// <summary>The profile's few words for a voice: how they talk.</summary>
        public static string VoiceWords(string voice)
        {
            switch (voice)
            {
                case Voice.Blunt: return "Says what they think, bluntly";
                case Voice.Warm: return "Talks warmly, even with bad news";
                case Voice.Chatty: return "Talks a lot, about everyone";
                case Voice.Guarded: return "Keeps their answers short";
                case Voice.Desperate: return "Sounds desperate";
                default: return "Talks plainly";
            }
        }

        /// <summary>A vote line, unfilled: the voice's slot for the manner (a voice without the slot speaks Plain), line <paramref name="index"/>.</summary>
        public static string VoteLine(string voice, string manner, int index)
        {
            var lines = Slot(voice, manner);
            return lines[Math.Max(0, Math.Min(lines.Length - 1, index))];
        }

        /// <summary>
        /// A line filled when it is made, because it is logged: <c>{NAME}</c> becomes the stated person's first name, or "you"
        /// for the player, capitalised where it opens the line. A line with nobody to name is returned as it is.
        /// </summary>
        public static string Fill(EpisodeState s, string line, string statedId)
        {
            if (line == null || line.IndexOf(Name, StringComparison.Ordinal) < 0) return line;
            string who = statedId == null ? "somebody" : statedId == s.playerId ? "you" : FinalistRead.FirstName(s.Find(statedId)?.name ?? statedId);
            if (line.StartsWith(Name, StringComparison.Ordinal) && who.Length > 0)
                line = char.ToUpperInvariant(who[0]) + who.Substring(1) + line.Substring(Name.Length);
            return line.Replace(Name, who);
        }

        /// <summary>The key of the vote line's coin: <c>w{week}:answer:vote:{voter}</c>.</summary>
        public static string VoteLineKey(int week, string voterId) => "w" + week + ":answer:vote:" + voterId;

        /// <summary>The vote answer a voter gave in a week, filled: the line <see cref="VoteLineIndex"/> gives, the same on every read.</summary>
        public static string VoteAnswer(EpisodeState s, int week, ContestantState voter, string manner, string statedId) =>
            Fill(s, VoteLine(Voice.Of(voter, false), manner, VoteLineIndex(s, week, voter, manner)), statedId);

        /// <summary>
        /// Which line of the voice's slot for the manner a voter's answer in a week takes. Below story rules 12 it is the coin's
        /// (<see cref="VoteLineKey"/>), as it always was. At 12 the answer's own record (<see cref="RecordedLine"/>) gives it
        /// once the answer is on the record, so every read quotes the line the log gave. Before that it is the coin's line,
        /// skipped while it is taken and the slot still has a line that is not, in the slot's order from the coin's: taken this
        /// week by an answer already on the record in the same slot (the same voice and manner), or by this voter's own answer
        /// last week in that slot. When every line is taken, a line nobody has said this week comes first, then the coin's.
        /// Nothing is drawn but the coin, a keyed hash of the story's stream, so the season's stream is never touched.
        /// </summary>
        public static int VoteLineIndex(EpisodeState s, int week, ContestantState voter, string manner)
        {
            int coin = StoryRandom.Index(s, VoteLineKey(week, voter.id), LinesASlot);
            if (!EpisodeEngine.StoryAt(s, StoryRules.Manners)) return coin;
            string voice = Voice.Of(voter, false);
            var own = RecordedLine(s, voter.id, week);
            if (own != null && own.manner == manner) return Math.Max(0, Math.Min(LinesASlot - 1, own.index));
            var slot = Slot(voice, manner);
            var thisWeek = new HashSet<int>();
            foreach (var row in s.story.cooldowns)
            {
                var other = ParseLine(row?.key);
                if (other == null || other.week != week || other.voterId == voter.id) continue;
                var who = s.Find(other.voterId);
                if (who != null && ReferenceEquals(Slot(Voice.Of(who, false), other.manner), slot)) thisWeek.Add(other.index);
            }
            var last = week > 1 ? RecordedLine(s, voter.id, week - 1) : null;
            int lastWeek = last != null && ReferenceEquals(Slot(voice, last.manner), slot) ? last.index : -1;
            int count = slot.Length;
            for (int step = 0; step < count; step++)
            {
                int index = (coin + step) % count;
                if (!thisWeek.Contains(index) && index != lastWeek) return index;
            }
            for (int step = 0; step < count; step++)
            {
                int index = (coin + step) % count;
                if (!thisWeek.Contains(index)) return index;
            }
            return coin;
        }

        /// <summary>The record of the line an answer took: <c>line:{week}:{voter}:{manner}:{index}</c>.</summary>
        public static string LineRecordKey(int week, string voterId, string manner, int index) =>
            EpisodeEngine.LinePrefix + week + ":" + voterId + ":" + manner + ":" + index;

        /// <summary>A line record, read from its key.</summary>
        public sealed class LineRecord
        {
            public int week, index;
            public string voterId, manner;
        }

        /// <summary>The record of the line a voter's answer took in a week; null when there is none.</summary>
        public static LineRecord RecordedLine(EpisodeState s, string voterId, int week)
        {
            string prefix = EpisodeEngine.LinePrefix + week + ":" + voterId + ":";
            var key = s?.story?.cooldowns?.FirstOrDefault(c => c?.key != null && c.key.StartsWith(prefix, StringComparison.Ordinal))?.key;
            return ParseLine(key);
        }

        /// <summary>A <c>line:</c> key's parts, or null for any other key or a malformed one.</summary>
        public static LineRecord ParseLine(string key)
        {
            if (key == null || !key.StartsWith(EpisodeEngine.LinePrefix, StringComparison.Ordinal)) return null;
            var parts = key.Split(':');
            if (parts.Length != 5 || !int.TryParse(parts[1], out int week) || parts[2].Length == 0 || !Manner.IsKnown(parts[3])
                || !int.TryParse(parts[4], out int index) || index < 0 || index >= LinesASlot)
                return null;
            return new LineRecord { week = week, voterId = parts[2], manner = parts[3], index = index };
        }

        /// <summary>The voice's slot for the manner, resolved as <see cref="VoteLine"/> resolves it: one array per slot.</summary>
        private static string[] Slot(string voice, string manner)
        {
            if (!VoteBank.TryGetValue(voice ?? Voice.Plain, out var slots) || !slots.ContainsKey(manner ?? "")) slots = VoteBank[Voice.Plain];
            if (!slots.TryGetValue(manner ?? "", out var lines)) lines = slots[Manner.NeutralFor(voice)];
            return lines;
        }
    }
}
