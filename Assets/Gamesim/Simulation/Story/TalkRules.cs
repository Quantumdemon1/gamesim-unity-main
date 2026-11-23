using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The free "Just talk" feature's own ladder (the lead's ruling R3): one saved level a season starts on,
    /// <c>story.talkRules</c>, in the pattern of <see cref="StoryRules"/> and <see cref="DialogueRules"/>. Level 0 is off.
    ///
    /// <para><see cref="Latest"/> is the bound validation holds a season to, never a gate and never what a season is
    /// started on. A level is defined here before its slice reads it, and admitted only from the schema that introduced it
    /// (<see cref="Admits"/>: every level from 31). A fresh season's level is written in
    /// <see cref="ShippedRules.ApplyFresh(EpisodeState)"/> and only there, as <see cref="Shipped"/>; each level is ENROLLED
    /// (moved into Shipped) only by its slice's integrator, as the slice lands (ruling R16; levels 1, 2 and 3 are enrolled). Load, migration,
    /// recovery and the importer never infer a level: an old save migrates at 0 and keeps free talk off. Every rule reads
    /// the level that introduced it as a lower bound (<see cref="At"/>), never <see cref="Latest"/>.</para>
    /// </summary>
    public static class TalkRules
    {
        /// <summary>No free talk: every season before schema 31, every old save, and every season the game started before level 1's enrolment.</summary>
        public const int Off = 0;

        /// <summary>Slice S1: the free talk on every housemate, its window cap, rapport tenths, the insight layers and where they stand.</summary>
        public const int Talk = 1;

        /// <summary>
        /// Slice S2: chemistry from your answers (<see cref="TalkChemistry"/>), the player's own read from the lean of their tones
        /// (<see cref="TalkLean"/>, the one field it writes: <c>story.talkLean</c>), and the friendship stages with their stage talks,
        /// the favour, a friend's honesty and the cost of nominating one (<see cref="TalkFriendship"/>).
        /// </summary>
        public const int Chemistry = 2;

        /// <summary>
        /// Slice S3: romance with the player (<see cref="TalkRomance"/>: the Flirt answer, interest, spark, the declaration both
        /// ways, endings, jealousy), the three stances with the All-Stars' turn-asides and hold-backs, and the house's reaction to
        /// a couple; also the earlier read of the Friends named (T9). Everything romantic also needs <c>story.romanceStorylines</c>.
        /// </summary>
        public const int Romance = 3;

        /// <summary>Slice S4: couples among the housemates.</summary>
        public const int Couples = 4;

        /// <summary>Slice S5: housemates who come to talk, and the long tail.</summary>
        public const int ComesToTalk = 5;

        /// <summary>The last level this build defines: a bound, never a behaviour.</summary>
        public const int Latest = ComesToTalk;

        /// <summary>
        /// The level a season the game starts plays under (<see cref="ShippedRules.ApplyFresh(EpisodeState)"/>): level 3,
        /// <see cref="Romance"/>, enrolled by slice 3's integrator as the slice lands (ruling R16, which replaces R4's gate: the
        /// balance arm runs afterwards and tunes the gains). It was <see cref="Off"/> from schema 31, then <see cref="Talk"/>
        /// from slice 1's enrolment and <see cref="Chemistry"/> from slice 2's; a season keeps the level it started on. Never
        /// <see cref="Latest"/>: each later level is enrolled only by its own slice's integrator.
        /// </summary>
        public const int Shipped = Romance;

        /// <summary>The schema that introduced the ladder and every level on it.</summary>
        public const int Schema = 31;

        /// <summary>The closed set this build defines: 0 to <see cref="Latest"/>.</summary>
        public static bool IsDefined(int level) => level >= Off && level <= Latest;

        /// <summary>The schema that introduced a level: 31 for every level from 1. Level 0 is every schema's.</summary>
        public static int SchemaFor(int level) => level >= Talk ? Schema : 0;

        /// <summary>Whether a season at <paramref name="schema"/> may hold talk level <paramref name="level"/>.</summary>
        public static bool Admits(int level, int schema) => IsDefined(level) && schema >= SchemaFor(level);

        /// <summary>Whether a season plays a level's rules: its saved level is that level or later. A lower bound.</summary>
        public static bool At(EpisodeState s, int level) => s?.story != null && s.story.talkRules >= level && level >= Talk;

        // ------------------------------------------------------------------ the saved fields' closed vocabularies and bounds

        /// <summary>Tones a counted talk can be answered in (spec 3.2): the saved lastTone and priorTone.</summary>
        public const string Warm = "warm", Straight = "straight", Tease = "tease", Push = "push", Careful = "careful",
            LetGo = "letgo", CallOut = "callout", Flirt = "flirt";
        public static readonly string[] Tones = { Warm, Straight, Tease, Push, Careful, LetGo, CallOut, Flirt };
        public static bool IsTone(string tone) => tone != null && Array.IndexOf(Tones, tone) >= 0;

        /// <summary>Where a romance with one housemate stands (spec 3.9, ruling R8's "Just friends" as friends).</summary>
        public const string Flirting = "flirting", Declared = "declared", Couple = "couple", Ended = "ended", Friends = "friends", Closed = "closed";
        public static readonly string[] Romances = { Flirting, Declared, Couple, Ended, Friends, Closed };
        public static bool IsRomance(string romance) => romance != null && Array.IndexOf(Romances, romance) >= 0;

        /// <summary>One-off marks the later slices read (spec 3.8 to 3.13).</summary>
        public const string RunningJoke = "running-joke", Pushy = "pushy", TookTheNoWell = "took-the-no-well", FlirtRemoved = "flirt-removed",
            Ex = "ex", FavourKept = "favour-kept", FavourRefused = "favour-refused", Jealous = "jealous", MayHear = "may-hear",
            CoupleTalk = "couple-talk", Warned = "warned";
        public static readonly string[] Flags =
            { RunningJoke, Pushy, TookTheNoWell, FlirtRemoved, Ex, FavourKept, FavourRefused, Jealous, MayHear, CoupleTalk, Warned };
        public static bool IsFlag(string flag) => flag != null && Array.IndexOf(Flags, flag) >= 0;

        /// <summary>Bounds of the saved fields (FIELDS.md): a spent-talk list, a talk id, a flag list, the pair list.</summary>
        public const int MostSpentTalks = 128, MostIdLength = 64, MostFlags = 16, MostPairs = 120, MostStandingLength = 256;

        /// <summary>
        /// One entry of <c>contact.spentTalks</c>: the talk's id, then, when the player answered it, a colon and the tone they
        /// answered in ("<c>casey.l1.bywater:warm</c>"). The tone is what a later callback talk's gate reads (spec 5.3,
        /// <c>callback=&lt;talk id&gt;:&lt;tone&gt;</c>); a talk with no answer is spent as its bare id. A talk id holds no colon.
        /// </summary>
        public const char ToneMark = ':';

        /// <summary>The spent entry for a talk answered in a tone, or its bare id when there was no answer.</summary>
        public static string Spent(string id, string tone) => string.IsNullOrEmpty(tone) ? id : id + ToneMark + tone;

        /// <summary>The talk id of a spent entry.</summary>
        public static string SpentId(string entry)
        {
            int at = entry.IndexOf(ToneMark);
            return at < 0 ? entry : entry.Substring(0, at);
        }

        /// <summary>The tone a spent talk was answered in, or "" when it had no answer.</summary>
        public static string SpentTone(string entry)
        {
            int at = entry.IndexOf(ToneMark);
            return at < 0 ? "" : entry.Substring(at + 1);
        }

        /// <summary>Whether a spent entry is a talk id of 1 to <see cref="MostIdLength"/> characters, then nothing or a colon and a tone.</summary>
        public static bool IsSpentEntry(string entry)
        {
            if (entry == null) return false;
            string id = SpentId(entry);
            return id.Length >= 1 && id.Length <= MostIdLength && (id.Length == entry.Length || IsTone(SpentTone(entry)));
        }

        /// <summary>The largest counted-talk, flirt and anchor counts a season can reach (808 windows in 101 weeks).</summary>
        public const int MostCount = 1000;

        /// <summary>A week written for "for the season": past any week a season reaches (validation's 101).</summary>
        public const int ForTheSeason = 102;

        /// <summary>The lean of the player's answers (spec 3.6): -6 to 6.</summary>
        public const int MostLean = 6;

        /// <summary>The agreement of your answers with one housemate (spec 3.3, ruling R10): -10 to 10.</summary>
        public const int MostAgreement = 10;

        /// <summary>
        /// The window key (ruling R5): 1 on move-in night, otherwise week * 8 + window + 2, so move-in night and week one's
        /// after-eviction window differ. The largest key a season at <paramref name="week"/> can hold.
        /// </summary>
        public static int LastWindowKey(int week) => week * 8 + 5;
    }
}
